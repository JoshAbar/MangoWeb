using System.Collections.Generic;

namespace HexaShop.WebApplication.Models.APIViewModels
{
    public class CoursesResult
    {
        public int Count { get; set; }
        public List<Course> Courses { get; set; }
    }
}