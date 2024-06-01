using System.Data.SqlClient;
using System.Net;
using Mango.Payment.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Stripe;

namespace Mango.Payment.Functions
{
    public class UpdateOrderStatusFunction
    {
        private readonly ILogger<UpdateOrderStatusFunction> _logger;
        private static string _sqlServerConnectionString;

        public UpdateOrderStatusFunction(ILogger<UpdateOrderStatusFunction> logger)
        {
            _logger = logger;
            _sqlServerConnectionString = Environment.GetEnvironmentVariable("SqlServerConnectionString");
        }

        [Function(nameof(UpdateOrderStatusFunction))]
        public async Task Run([ServiceBusTrigger("update-order-status", "sub1", Connection = "ServiceBusConnectionString")] string message)
        {
            _logger.LogInformation("Message data: {message}", message);
            var messageBody = JsonConvert.DeserializeObject<UpdateOrderStatusMessage>(message);

            var orderHeader = await GetOrderAsync(messageBody.OrderId);
            if (orderHeader != null)
            {
                if (messageBody.Status == "Cancelled")
                {
                    StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("StripeSecretKey");

                    var options = new RefundCreateOptions
                    {
                        Reason = RefundReasons.RequestedByCustomer,
                        PaymentIntent = orderHeader.PaymentIntentId
                    };

                    var service = new RefundService();
                    var refund = await service.CreateAsync(options);
                    // Logic for Refund
                }

                orderHeader.Status = messageBody.Status;
                await UpdateStatusAsync(orderHeader);
            }
        }

        private async Task<OrderHeaderDto?> GetOrderAsync(int orderHeaderId)
        {
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
            con.Close();
            return orderHeader ?? default!;
        }

        private async Task UpdateStatusAsync(OrderHeaderDto orderHeader)
        {
            var con = new SqlConnection(_sqlServerConnectionString);
            con.Open();
            var cmd = new SqlCommand("UPDATE OrderHeaders SET Status = @Status WHERE OrderHeaderId = @OrderHeaderId", con);
            cmd.Parameters.AddWithValue("@Status", orderHeader.Status);
            cmd.Parameters.AddWithValue("@OrderHeaderId", orderHeader.OrderHeaderId);
            await cmd.ExecuteNonQueryAsync();
            con.Close();
        }
    }
}
