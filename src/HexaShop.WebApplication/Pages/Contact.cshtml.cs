using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HexaShop.WebApplication.Pages
{
    public class ContactModel : PageModel
    {
        public string Message { get; set; }

        public void OnGet()
        {
            Message = "Thank You!! <3";
        }
    }
}
