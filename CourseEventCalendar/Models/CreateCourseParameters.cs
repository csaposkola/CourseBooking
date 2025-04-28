using System;

namespace CourseEventCalendar.CourseEventCalendar.Models
{
    public class CreateCourseParameters
    {
        public int TemplateID { get; set; }

        public DateTime StartAt { get; set; }
    }
}