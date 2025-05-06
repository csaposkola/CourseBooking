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
        
        [HttpPost]
        [DnnAuthorize]
        public HttpResponseMessage AddToCart(string productId)
        {
            try
            {
                if (string.IsNullOrEmpty(productId))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Product ID is required");
                }

                // Construct the add to cart URL - this is a standard Hotcakes URL
                string addToCartUrl = $"/AddToCart.aspx?ProductId={productId}&Quantity=1";
                
                return Request.CreateResponse(HttpStatusCode.OK, new { success = true, cartUrl = addToCartUrl });
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }
}