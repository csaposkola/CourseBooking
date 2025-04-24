using DotNetNuke.Web.Api;

namespace CourseBooking.Services
{
    public class RouteMapper : IServiceRouteMapper
    {
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute(
                "CourseBooking",
                "default",
                "{controller}/{action}",
                new[] { "CourseBooking.Controllers" }
            );
        }
    }
}