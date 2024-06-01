using System;
using System.Data.SqlClient;
using Azure;
using Mango.Payment.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stripe.Checkout;
using Stripe;
using System.Text;
using Microsoft.Azure.ServiceBus;

namespace Mango.Payment.Functions
{
    public class CreateStripeSessionFunction
    {
        private readonly ILogger<CreateStripeSessionFunction> _logger;

        public CreateStripeSessionFunction(ILogger<CreateStripeSessionFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(CreateStripeSessionFunction))]
        [ServiceBusOutput("order-payment", Connection = "ServiceBusConnectionString")]
        public async Task<object> Run(
            [ServiceBusTrigger("create-stripe-session", "sub1", Connection = "ServiceBusConnectionString")]
            string message)
        {
            _logger.LogInformation("Message ID: {id}", message);
            var stripeRequestDto = JsonConvert.DeserializeObject<StripeRequestDto>(message);
            _logger.LogInformation("OrderHeaderId: {id}", stripeRequestDto.OrderHeader?.OrderHeaderId);

            const string domain = "http://localhost:7167/";
            StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("StripeSecretKey");

            var options = new SessionCreateOptions()
            {
                SuccessUrl = stripeRequestDto.ApprovedUrl,
                CancelUrl = stripeRequestDto.CancelUrl,
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",

            };

            var discountObj = new List<SessionDiscountOptions>()
            {
                new SessionDiscountOptions
                {
                    Coupon = stripeRequestDto.OrderHeader?.CouponCode
                }
            };
            if (stripeRequestDto.OrderHeader?.OrderDetails != null)
                foreach (var item in stripeRequestDto.OrderHeader?.OrderDetails)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price * 100), // $20.99 -> 2099
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product?.Name
                            }
                        },
                        Quantity = item.Count
                    };

                    options.LineItems.Add(sessionLineItem);
                }

            if (stripeRequestDto.OrderHeader?.Discount > 0)
            {
                options.Discounts = discountObj;
            }

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            var response = JsonConvert.SerializeObject(new
            {
                StripeSessionUrl = session.Url,
                StripeSessionId = session.Id,
                OrderHeader = stripeRequestDto.OrderHeader
            });

            _logger.LogInformation("Response: {response}", response);

            return response;
        }
    }
}
