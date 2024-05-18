using System.Collections.Generic;

namespace HexaShop.WebApplication.Models.APIViewModels
{
    public class InstructorResult : PageableResult
    {
        public List<Instructor> Instructors { get; set; }
    }
}
