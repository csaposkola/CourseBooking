using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Csaposkola.Modules.Kurzusnaptar.Components;
using DotNetNuke.Web.Api;

namespace Csaposkola.Modules.Kurzusnaptar.Controllers
{
    [AllowAnonymous]
    [DnnAuthorize]
    public class CourseApiController : DnnApiController
    {
        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage GetCourses()
        {
            try
            {
                var events = CourseEventManager.Instance.GetCourseEvents();
                return Request.CreateResponse(HttpStatusCode.OK, events);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}