using System.Collections.Generic;
using System.Linq;

namespace HexaShop.API.DTO
{
    public class CourseStudentResult
    {
        public int Count => Courses.Count();
        public IList<Course> Courses { get; set; }

        public CourseStudentResult()
        {
            Courses = new List<Course>();
        }
    }
}