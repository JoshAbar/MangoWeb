using IdentityModel;
using Mango.Web.Models;
using Mango.Web.Service.IService;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;

namespace Mango.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProductService _productService;
        private readonly ICartService _cartService;
        private readonly IDistributedCache _distributedCache;
        public HomeController(IProductService productService, ICartService cartService, IDistributedCache distributedCache)
        {
            _productService = productService;
            _cartService = cartService;
            _distributedCache = distributedCache;
        }

        public async Task<IActionResult> Index()
        {
            List<ProductDto>? list = new();
            var keyCaching = "home-products";

            var bytes = await _distributedCache.GetAsync(keyCaching);
            if (bytes != null)
            {
                string cachedValue = Encoding.UTF8.GetString(bytes);
                list = JsonConvert.DeserializeObject<List<ProductDto>>(cachedValue);
            }
            else
            {
                ResponseDto? response = await _productService.GetAllProductsAsync();
                
                DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = (DateTime.UtcNow.AddMinutes(30)),
                    SlidingExpiration = (TimeSpan.FromMinutes(2))
                };
                await _distributedCache.SetStringAsync(keyCaching, JsonConvert.SerializeObject(response.Result), cacheOptions);

                if (response != null && response.IsSuccess)
                {
                    list = JsonConvert.DeserializeObject<List<ProductDto>>(Convert.ToString(response.Result));
                }
                else
                {
                    TempData["error"] = response?.Message;
                }
            }

            return View(list);
        }

        [Authorize]
        public async Task<IActionResult> ProductDetails(int productId)
        {
            ProductDto? model = new();

            ResponseDto? response = await _productService.GetProductByIdAsync(productId);

            if (response != null && response.IsSuccess)
            {
                model = JsonConvert.DeserializeObject<ProductDto>(Convert.ToString(response.Result));
            }
            else
            {
                TempData["error"] = response?.Message;
            }

            return View(model);
        }


        [Authorize]
        [HttpPost]
        [ActionName("ProductDetails")]
        public async Task<IActionResult> ProductDetails(ProductDto productDto)
        {
            var userId = User.Claims.Where(u => u.Type == JwtClaimTypes.Subject)?.FirstOrDefault()?.Value;
            await _distributedCache.RemoveAsync($"cart-{userId}");
            CartDto cartDto = new CartDto()
            {
                CartHeader = new CartHeaderDto
                {
                    UserId = userId
                }
            };

            CartDetailsDto cartDetails = new CartDetailsDto()
            {
                Count = productDto.Count,
                ProductId = productDto.ProductId,
            };

            List<CartDetailsDto> cartDetailsDtos = new() { cartDetails};
            cartDto.CartDetails = cartDetailsDtos;

            ResponseDto? response = await _cartService.UpsertCartAsync(cartDto);

            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Item has been added to the Shopping Cart";
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["error"] = response?.Message;
            }

            return View(productDto);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}