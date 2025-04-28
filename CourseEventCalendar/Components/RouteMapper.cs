using DotNetNuke.Web.Api;
using System.Diagnostics;

namespace CourseEventCalendar.CourseEventCalendar.Components
{
    public class RouteMapper : IServiceRouteMapper
    {
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute(
                "CourseEventCalendar",
                "default",
                "{controller}/{action}",
                new string[] { "CourseEventCalendar.CourseEventCalendar.Controllers.Api" });
        }
    }
}