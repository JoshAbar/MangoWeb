
using System.Text.Json.Serialization;

namespace Mango.Web.Models
{
    public class OrderDetailsDto
    {
        public int OrderDetailsId { get; set; }
        public int OrderHeaderId { get; set; }
        public int ProductId { get; set; }
        
        [JsonPropertyName("product")]
        public ProductDto? Product { get; set; }
        
        [JsonPropertyName("count")]
        public int Count { get; set; }
        public string ProductName { get; set; }
        
        [JsonPropertyName("price")]
        public double Price { get; set; }
    }
}
