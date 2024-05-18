using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace HexaShop.WebApplication.Pages
{
    public class TestProduct : PageModel
    {
        public IConfiguration Configuration { get; }


        public TestProduct(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public void OnGet()
        {
            var section = Configuration.GetSection("Infos");
            ViewData["Ambiente"] = section["Ambiente"].ToString();
            ViewData["Versao"] = section["Versao"].ToString();
        }
    }
}