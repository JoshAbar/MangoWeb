using System.Collections.Generic;
namespace HexaShop.API.DTO
{
    public class InstructorResult : PageableResult
    {
        public IList<Instructor> Instructors { get; set; }

        public InstructorResult()
        {
            Instructors = new List<Instructor>();
        }
    }
}
