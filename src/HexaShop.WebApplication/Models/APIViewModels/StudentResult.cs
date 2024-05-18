using System.Collections.Generic;

namespace HexaShop.WebApplication.Models.APIViewModels
{
    public class StudentResult : PageableResult
    {

        public List<Student> Students { get; set; }
    }
}