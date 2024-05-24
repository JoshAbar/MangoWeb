using System.Text.Json.Serialization;

namespace Mango.Web.Models
{
    public class OrderHeaderDto
    {
        [JsonPropertyName("orderHeaderId")]
        public int OrderHeaderId { get; set; }
        public string? UserId { get; set; }
        
        [JsonPropertyName("couponCode")]
        public string? CouponCode { get; set; }
        
        [JsonPropertyName("discount")]
        public double Discount { get; set; }
        public double OrderTotal { get; set; }


        public string? Name { get; set; }

        public string? Phone { get; set; }

        public string? Email { get; set; }
        public DateTime OrderTime { get; set; }
        public string? Status { get; set; }
        public string? PaymentIntentId { get; set; }
        
        public string? StripeSessionId { get; set; }
        
        [JsonPropertyName("orderDetails")]
        public IEnumerable<OrderDetailsDto> OrderDetails { get; set; }
    }
}
