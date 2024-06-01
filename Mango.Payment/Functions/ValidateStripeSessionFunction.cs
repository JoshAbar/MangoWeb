using System.Data.SqlClient;
using System.Net;
using System.Text;
using Azure;
//using Azure.Messaging.ServiceBus;
using Mango.Payment.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stripe;
using Stripe.Checkout;

namespace Mango.Payment.Functions
{
    public class ValidateStripeSessionFunction
    {
        private readonly ILogger<ValidateStripeSessionFunction> _logger;
        private static string _sqlServerConnectionString;
        public ValidateStripeSessionFunction(ILogger<ValidateStripeSessionFunction> logger)
        {
            _logger = logger;
            _sqlServerConnectionString = Environment.GetEnvironmentVariable("SqlServerConnectionString");
        }

        [Function(nameof(ValidateStripeSessionFunction))]
        public async Task Run([ServiceBusTrigger("validate-stripe-session", "sub1", Connection = "ServiceBusConnectionString")] string message)
        {
            _logger.LogInformation("Message data:", message);
            var orderHeaderId = JsonConvert.DeserializeObject<OrderHeaderDto>(message).OrderHeaderId;
            var con = new SqlConnection(_sqlServerConnectionString);
            con.Open();
            var cmd = new SqlCommand("SELECT * FROM OrderHeaders WHERE OrderHeaderId = @OrderHeaderId", con);
            cmd.Parameters.AddWithValue("@OrderHeaderId", orderHeaderId);
            var reader = await cmd.ExecuteReaderAsync();

            OrderHeaderDto orderHeader = null;
            if (reader.HasRows)
            {
                reader.Read(); 
                orderHeader = new OrderHeaderDto
                {
                    OrderHeaderId = Convert.ToInt32(reader["OrderHeaderId"]),
                    UserId = Convert.ToString(reader["UserId"]),
                    CouponCode = Convert.ToString(reader["CouponCode"]),
                    Discount = Convert.ToDouble(reader["Discount"]),
                    OrderTotal = Convert.ToDouble(reader["OrderTotal"]),
                    Status = Convert.ToString(reader["Status"]),
                    OrderTime = Convert.ToDateTime(reader["OrderTime"]),
                    PaymentIntentId = Convert.ToString(reader["PaymentIntentId"]),
                    StripeSessionId = Convert.ToString(reader["StripeSessionId"]),
                };
                await reader.CloseAsync();
            }

            StripeConfiguration.ApiKey =
                Environment.GetEnvironmentVariable("StripeSecretKey");

            var service = new SessionService();
            var session = await service.GetAsync(orderHeader?.StripeSessionId);

            var paymentIntentService = new PaymentIntentService();
            var paymentIntent = await paymentIntentService.GetAsync(session.PaymentIntentId);

            if (paymentIntent.Status == "succeeded")
            {
                orderHeader.Status = "Approved";
                orderHeader.PaymentIntentId = paymentIntent.Id;

                cmd = new SqlCommand("UPDATE OrderHeaders SET PaymentIntentId = @PaymentIntentId, Status = @Status WHERE OrderHeaderId = @OrderHeaderId", con);
                cmd.Parameters.AddWithValue("@PaymentIntentId", orderHeader.PaymentIntentId);
                cmd.Parameters.AddWithValue("@Status", orderHeader.Status);
                cmd.Parameters.AddWithValue("@OrderHeaderId", orderHeader.OrderHeaderId);
                await cmd.ExecuteNonQueryAsync();

                con.Close();
            }

        }
    }
}
