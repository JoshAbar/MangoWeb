using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace HexaShop.WebApplication.Pages
{
    public class TestProducts : PageModel
    {
        public IConfiguration Configuration { get; }


        public TestProducts(IConfiguration configuration)
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