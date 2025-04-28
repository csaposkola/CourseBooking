using CourseEventCalendar.CourseEventCalendar.Services;
using System;
using System.Web.Http;
using System.Net.Http;

namespace CourseEventCalendar.CourseEventCalendar.Controllers.Api
{
    public class TemplateController : CourseApiControllerBase
    {
        public TemplateController(
            ICourseEventManager courseManager
            ) : base(courseManager) { }

        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage List()
        {
            try
            {
                var templates = CourseManager.FindCourseTemplates(
                    UserInfo.IsAdmin
                    );

                return Json(new { templates });
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }
    }
}