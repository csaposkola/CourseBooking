using DotNetNuke.Web.Api;

namespace Csaposkola.Modules.Kurzusnaptar.Components
{
    public class FeatureController : IServiceRouteMapper
    {
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute(
                "Kurzusnaptar",
                "default",
                "{controller}/{action}",
                new[] { "Csaposkola.Modules.Kurzusnaptar.Controllers" }
            );
        }
    }
}