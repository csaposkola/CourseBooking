using CourseEventCalendar.CourseEventCalendar.Services;
using System;

namespace CourseEventCalendar.CourseEventCalendar.Controllers.Api
{
    public class CourseApiControllerBase : RestApiControllerBase
    {
        protected ICourseEventManager CourseManager { get; }

        public CourseApiControllerBase(
            ICourseEventManager courseManager
            )
        {
            CourseManager = courseManager
                ?? throw new ArgumentNullException(nameof(courseManager));
        }
    }
}