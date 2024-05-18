using System.Collections.Generic;

namespace HexaShop.WebApplication.Models.APIViewModels
{
    public class DepartmentResult
    {
        public int Count { get; set; }
        public List<Department> Departments { get; set; }
    }
}
