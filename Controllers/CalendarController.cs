// Controllers/CalendarController.cs
using System.Web.Mvc;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using DotNetNuke.Framework.JavaScriptLibraries;

namespace Csaposkola.Modules.Kurzusnaptar.Controllers
{
    [DnnHandleError]
    public class CalendarController : DnnController
    {
        public ActionResult Index()
        {
            // Register needed scripts
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.DnnPlugins);
            DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxScriptSupport();
            
            // Ensure the framework knows this is a view
            return View();
        }
    }
}