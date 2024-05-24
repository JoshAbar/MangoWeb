using Mango.Web.Models;
using Mango.Web.Service;
using Mango.Web.Service.IService;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Caching.Distributed;

namespace Mango.Web.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IOrderService _orderService;
        private static string connectionString;
        private static string createStripeSessionTopic;
        private static string validateStripeSessionTopic;
        private readonly IDistributedCache _distributedCache;
        public CartController(ICartService cartService, IOrderService orderService, IConfiguration configuration, IDistributedCache distributedCache)
        {
            _cartService = cartService;
            _orderService = orderService;
            _distributedCache = distributedCache;
            connectionString = configuration.GetValue<string>("ServiceBusConnectionString");
            createStripeSessionTopic = "create-stripe-session";
            validateStripeSessionTopic = "validate-stripe-session";
        }

        [Authorize]
        public async Task<IActionResult> CartIndex()
        {
            return View(await LoadCartDtoBasedOnLoggedInUser());
        }

        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            return View(await LoadCartDtoBasedOnLoggedInUser());
        }
        
        [HttpPost]
        [ActionName("Checkout")]
        public async Task<IActionResult> Checkout(CartDto cartDto)
        {

            CartDto cart = await LoadCartDtoBasedOnLoggedInUser();
            cart.CartHeader.Phone = cartDto.CartHeader.Phone;
            cart.CartHeader.Email = cartDto.CartHeader.Email;
            cart.CartHeader.Name = cartDto.CartHeader.Name;

            var response = await _orderService.CreateOrder(cart);
            OrderHeaderDto? orderHeaderDto = JsonConvert.DeserializeObject<OrderHeaderDto>(Convert.ToString(response.Result));
            
            var domain = Request.Scheme + "://" + Request.Host.Value + "/";
            StripeRequestDto stripeRequestDto = new()
            {
                ApprovedUrl = domain + "cart/Confirmation?orderId=" + orderHeaderDto.OrderHeaderId,
                CancelUrl = domain + "cart/checkout",
                OrderHeader = orderHeaderDto
            };
            
            var topicClient = new TopicClient(connectionString, createStripeSessionTopic);
            try
            {
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(stripeRequestDto)));
                await topicClient.SendAsync(message);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            await topicClient.CloseAsync();
            
            if (response != null && response.IsSuccess)
            {

                var stripeResponse = await _orderService.CreateStripeSession(stripeRequestDto);
                StripeRequestDto? stripeResponseResult = JsonConvert.DeserializeObject<StripeRequestDto>(Convert.ToString(stripeResponse?.Result) ?? "");
                Response.Headers.Add("Location", stripeResponseResult?.StripeSessionUrl);
                return new StatusCodeResult(303);
            }
            return View();
        }

        public async Task<IActionResult> Confirmation(int orderId)
        {
            var topicClient = new TopicClient(connectionString, validateStripeSessionTopic);
            try
            {
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    OrderHeaderId = orderId
                })));
                await topicClient.SendAsync(message);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            await topicClient.CloseAsync();
            ResponseDto? response = await _orderService.ValidateStripeSession(orderId);
            
            if (response != null & response.IsSuccess)
            {
                OrderHeaderDto orderHeader = JsonConvert.DeserializeObject<OrderHeaderDto>(Convert.ToString(response.Result));
                if (orderHeader.Status == SD.Status_Approved)
                {
                    return View(orderId);
                }
            }
            //redirect to some error page based on status
            return View(orderId);
        }

        public async Task<IActionResult> Remove(int cartDetailsId)
        {
            var userId = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            await _distributedCache.RemoveAsync($"cart-{userId}");
            ResponseDto? response = await _cartService.RemoveFromCartAsync(cartDetailsId);
            if (response != null & response.IsSuccess)
            {
                TempData["success"] = "Cart updated successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(CartDto cartDto)
        {
            
            ResponseDto? response = await _cartService.ApplyCouponAsync(cartDto);
            if (response != null & response.IsSuccess)
            {
                TempData["success"] = "Cart updated successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EmailCart(CartDto cartDto)
        {
            CartDto cart = await LoadCartDtoBasedOnLoggedInUser();
            cart.CartHeader.Email = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Email)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.EmailCart(cart);
            if (response != null & response.IsSuccess)
            {
                TempData["success"] = "Email will be processed and sent shortly.";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCoupon(CartDto cartDto)
        {
            cartDto.CartHeader.CouponCode = "";
            ResponseDto? response = await _cartService.ApplyCouponAsync(cartDto);
            if (response != null & response.IsSuccess)
            {
                TempData["success"] = "Cart updated successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return View();
        }


        private async Task<CartDto> LoadCartDtoBasedOnLoggedInUser()
        {
            var userId = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            var keyCaching = $"cart-{userId}";
            var bytes = await _distributedCache.GetAsync(keyCaching);
            if (bytes != null)
            {
                string cachedValue = Encoding.UTF8.GetString(bytes);
                return JsonConvert.DeserializeObject<CartDto>(cachedValue) ?? new CartDto();
            }
            
            ResponseDto? response = await _cartService.GetCartByUserIdAsnyc(userId);
            if(response !=null & response.IsSuccess)
            {
                CartDto cartDto = JsonConvert.DeserializeObject<CartDto>(Convert.ToString(response.Result));
                await _distributedCache.SetStringAsync(keyCaching, JsonConvert.SerializeObject(cartDto));
                
                return cartDto;
            }
            
            return new CartDto();
        }
    }
}
