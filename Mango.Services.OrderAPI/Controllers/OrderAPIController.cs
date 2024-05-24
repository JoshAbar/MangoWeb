using System.Text;
using AutoMapper;
using Azure.Messaging.ServiceBus;
using Mango.Services.OrderAPI.Data;
using Mango.Services.OrderAPI.Models;
using Mango.Services.OrderAPI.Models.Dto;
using Mango.Services.OrderAPI.Utility;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using Stripe;
using Mango.MessageBus;
using Microsoft.Azure.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Mango.Services.OrderAPI.Controllers
{
    [Route("api/order")]
    [ApiController]
    public class OrderAPIController : ControllerBase
    {
        protected ResponseDto _response;
        private IMapper _mapper;
        private readonly AppDbContext _db;
        private IProductService _productService;
        private readonly IMessageBus _messageBus;
        private readonly IConfiguration _configuration;
        private static string connectionString;
        private static string orderPaymentProcessTopic;
        private static string orderConfirmTopic;
        
        public OrderAPIController(AppDbContext db,
            IProductService productService, IMapper mapper, IConfiguration configuration
            ,IMessageBus messageBus)
        {
            _db = db;
            _messageBus = messageBus;
            this._response = new ResponseDto();
            _productService = productService;
            _mapper = mapper;
            _configuration = configuration;
            connectionString = _configuration.GetValue<string>("ServiceBusConnectionString");
            orderPaymentProcessTopic = "order-payment";
            orderConfirmTopic = "order-confirm";
        }

        [Authorize]
        [HttpGet("GetOrders")]
        public ResponseDto? Get(string? userId = "")
        {
            try
            {
                IEnumerable<OrderHeader> objList;
                if (User.IsInRole(SD.RoleAdmin))
                {
                    objList = _db.OrderHeaders.Include(u => u.OrderDetails).OrderByDescending(u => u.OrderHeaderId).ToList();
                }
                else
                {
                    objList = _db.OrderHeaders.Include(u => u.OrderDetails).Where(u=>u.UserId==userId).OrderByDescending(u => u.OrderHeaderId).ToList();
                }
                _response.Result = _mapper.Map<IEnumerable<OrderHeaderDto>>(objList);
            }
            catch (Exception ex) 
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [Authorize]
        [HttpGet("GetOrder/{id:int}")]
        public ResponseDto? Get(int id)
        {
            try
            {
                OrderHeader orderHeader = _db.OrderHeaders.Include(u => u.OrderDetails).First(u => u.OrderHeaderId == id);
                _response.Result = _mapper.Map<OrderHeaderDto>(orderHeader);
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
            }
            return _response;
        }

        [Authorize]
        [HttpPost("CreateOrder")]
        public async Task<ResponseDto> CreateOrder([FromBody] CartDto cartDto)
        {
            try
            {
                OrderHeaderDto orderHeaderDto = _mapper.Map<OrderHeaderDto>(cartDto.CartHeader);
                orderHeaderDto.OrderTime = DateTime.Now;
                orderHeaderDto.Status = SD.Status_Pending;
                orderHeaderDto.OrderDetails = _mapper.Map<IEnumerable<OrderDetailsDto>>(cartDto.CartDetails);
                orderHeaderDto.OrderTotal = Math.Round(orderHeaderDto.OrderTotal, 2);
                OrderHeader orderCreated = _db.OrderHeaders.Add(_mapper.Map<OrderHeader>(orderHeaderDto)).Entity;
                await _db.SaveChangesAsync();

                orderHeaderDto.OrderHeaderId = orderCreated.OrderHeaderId;
                _response.Result = orderHeaderDto;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message=ex.Message;
            }
            return _response;
        }


        [Authorize]
        [HttpPost("CreateStripeSession")]
        public async Task<ResponseDto> CreateStripeSession([FromBody] StripeRequestDto stripeRequestDto)
        {
            var responseDto = new ResponseDto();
            var client = new ServiceBusClient(connectionString);
            
            var options = new ServiceBusProcessorOptions 
            {

                AutoCompleteMessages = false,
                MaxConcurrentCalls = 20
            };
            var processor = client.CreateProcessor(orderPaymentProcessTopic, options);
            
            processor.ProcessMessageAsync += async (messageArgs) =>
            {
                var body = messageArgs.Message.Body.ToString();
                await messageArgs.CompleteMessageAsync(messageArgs.Message);
                    
                var deserializeObject = JsonConvert.DeserializeObject<StripeRequestDto>(body);

                await _db.OrderHeaders
                    .Where(order => order.OrderHeaderId == deserializeObject!.OrderHeader.OrderHeaderId)
                    .ExecuteUpdateAsync(setter =>
                        setter.SetProperty(order => order.StripeSessionId, deserializeObject.StripeSessionId));
                
                responseDto.Result = deserializeObject;
                responseDto.IsSuccess = true;
                await Task.CompletedTask;
            };
            processor.ProcessErrorAsync += async (error) =>
            {
                responseDto.Message = error.Exception.Message;
                responseDto.IsSuccess = false;
                
                await Task.CompletedTask;
            };

            await processor.StartProcessingAsync();

            if (responseDto.Result == null)
            {
                await processor.StopProcessingAsync();
                responseDto.Result = await CreateStripeSessionService(stripeRequestDto);
            }
            
            return responseDto;
        }


        [Authorize]
        [HttpPost("ValidateStripeSession")]
        public async Task<ResponseDto> ValidateStripeSession([FromBody] int orderHeaderId)
        {
            var orderHeader = _db.OrderHeaders.FirstOrDefault(order => order.OrderHeaderId == orderHeaderId);
            if (orderHeader == null)
                throw new Exception("Order header is null");

            var newReward = new RewardsDto
            {
                OrderId = orderHeader.OrderHeaderId,
                RewardsActivity = Convert.ToInt32(orderHeader.OrderTotal),
                UserId = orderHeader.UserId
            };
            
            var topicName = _configuration.GetValue<string>("TopicAndQueueNames:OrderCreatedTopic");
            var connStr = _configuration.GetValue<string>("ServiceBusConnectionString");
            await _messageBus.PublishMessage(newReward, topicName, connStr);
            _response.Result = _mapper.Map<OrderHeaderDto>(orderHeader);
            return _response;
        }


        [Authorize]
        [HttpPost("UpdateOrderStatus/{orderId:int}")]
        public async Task<ResponseDto> UpdateOrderStatus(int orderId, [FromBody] string newStatus)
        {
            try
            {
                OrderHeader orderHeader = _db.OrderHeaders.First(u => u.OrderHeaderId == orderId);
                if (orderHeader != null)
                {
                    if(newStatus == SD.Status_Cancelled)
                    {
                        //we will give refund
                        var options = new RefundCreateOptions
                        {
                            Reason = RefundReasons.RequestedByCustomer,
                            PaymentIntent = orderHeader.PaymentIntentId
                        };

                        var service = new RefundService();
                        Refund refund = service.Create(options);
                    }
                    orderHeader.Status = newStatus;
                    _db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
            }
            return _response;
        }

        private async Task<StripeRequestDto> CreateStripeSessionService(StripeRequestDto stripeRequestDto)
        {
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
            stripeRequestDto.StripeSessionId = session.Id;
            stripeRequestDto.StripeSessionUrl = session.Url;

           await UpdateStripeSession(stripeRequestDto.OrderHeader.OrderHeaderId, session.Id);
            
            return stripeRequestDto;
        }

        private async Task UpdateStripeSession(int orderId, string sessionId)
        {
            await _db.OrderHeaders
                .Where(order => order.OrderHeaderId == orderId)
                .ExecuteUpdateAsync(setter =>
                    setter.SetProperty(order => order.StripeSessionId, sessionId));
        }
    }
}
