using System.Collections.Generic;

namespace HexaShop.API.DTO
{
    public class StudentCourseResult : PageableResult
    {
        public IList<Student> Students { get; set; }

        public StudentCourseResult()
        {
            Students = new List<Student>();
        }
    }
}