using CourseBooking.Services;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CourseBooking.Controllers.Api
{
    public class CourseApiController : DnnApiController
    {
        private IBookingService _bookingService;

        // Direct constructor instantiation with try-catch for safety
        public CourseApiController()
        {
            try
            {
                // Try to initialize service in constructor
                TryInitializeService();
            }
            catch (Exception ex)
            {
                // Log but don't fail constructor
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
            }
        }

        private void TryInitializeService(int explicitModuleId = -1)
        {
            try
            {
                int moduleId = explicitModuleId;
                int portalId = -1;

                // First try with explicit moduleId if provided
                if (explicitModuleId > 0)
                {
                    portalId = PortalSettings?.PortalId ?? 0;
                    _bookingService = new BookingService(explicitModuleId, portalId);
                    return;
                }

                // Try to get module id from ActiveModule
                if (ActiveModule != null && PortalSettings != null)
                {
                    _bookingService = new BookingService(ActiveModule.ModuleID, PortalSettings.PortalId);
                    return;
                }

                // Try from request context
                moduleId = -1;
                portalId = -1;

                var request = Request;
                if (request != null)
                {
                    // Try from query string
                    if (request.GetQueryNameValuePairs().Any(p => p.Key.Equals("moduleid", StringComparison.OrdinalIgnoreCase)))
                    {
                        string moduleIdValue = request.GetQueryNameValuePairs()
                            .First(p => p.Key.Equals("moduleid", StringComparison.OrdinalIgnoreCase)).Value;
                        
                        if (!string.IsNullOrEmpty(moduleIdValue) && int.TryParse(moduleIdValue, out int mid))
                        {
                            moduleId = mid;
                        }
                    }
                }

                // Try from HTTP context
                if (moduleId <= 0)
                {
                    var httpContext = System.Web.HttpContext.Current;
                    if (httpContext != null)
                    {
                        var mid = httpContext.Request.QueryString["moduleid"];
                        if (!string.IsNullOrEmpty(mid) && int.TryParse(mid, out int parsedModuleId))
                        {
                            moduleId = parsedModuleId;
                        }
                    }
                }

                // Set portal ID
                if (PortalSettings != null)
                {
                    portalId = PortalSettings.PortalId;
                }
                else
                {
                    // Final fallback to default portal (0)
                    portalId = 0;
                }

                // Initialize service if we have valid IDs
                if (moduleId > 0 && portalId >= 0)
                {
                    _bookingService = new BookingService(moduleId, portalId);
                }
            }
            catch (Exception ex)
            {
                // Just log the error, don't throw
                DotNetNuke.Services.Exceptions.Exceptions.LogException(
                    new InvalidOperationException($"Error initializing service: Mid={explicitModuleId}", ex));
            }
        }

        protected IBookingService BookingService
        {
            get
            {
                if (_bookingService == null)
                {
                    // Try to initialize if null
                    TryInitializeService();
                }
                return _bookingService;
            }
        }

        private bool IsServiceAvailable(out HttpResponseMessage errorResponse)
        {
            if (BookingService == null) 
            {
                errorResponse = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, 
                    "Service unavailable. Please provide moduleId parameter.");
                return false;
            }
            errorResponse = null;
            return true;
        }

        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage GetCourseSchedules(int year, int month, int moduleId = -1)
        {
            // Try initializing the service with explicit moduleId
            if (moduleId > 0 && _bookingService == null)
            {
                TryInitializeService(moduleId);
            }

            if (!IsServiceAvailable(out var errorResponse))
            {
                return errorResponse;
            }

            if (year < 1900 || year > 2100 || month < 1 || month > 12)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid year or month specified.");
            }

            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Get only active schedules for public view
                var schedules = BookingService.GetCourseSchedules(startDate, endDate, includeInactive: false);

                // Determine user's registered schedules if logged in
                HashSet<int> userRegisteredScheduleIds = null;
                if (UserInfo != null && UserInfo.UserID > 0)
                {
                    userRegisteredScheduleIds = BookingService.GetBookingsByUser(UserInfo.UserID)
                        .Where(b => !b.IsCancelled)
                        .Select(b => b.CourseScheduleID)
                        .ToHashSet();
                }

                // Create simplified response with only necessary data
                var responseSchedules = schedules.Select(s => new
                {
                    s.ID,
                    s.CoursePlanID,
                    s.StartTime,
                    s.AvailableSeats,
                    s.IsActive,
                    CoursePlanName = s.CoursePlan?.Name ?? "Unknown Course",
                    CoursePlanCategory = s.CoursePlan?.CourseCategory ?? "Unknown",
                    s.BookingCount,
                    s.RemainingSeats,
                    IsUserRegistered = userRegisteredScheduleIds?.Contains(s.ID) ?? false
                }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, responseSchedules);
            }
            catch (Exception ex)
            {
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, 
                    "An error occurred retrieving course schedules: " + ex.Message);
            }
        }

        [HttpPost]
        [DnnAuthorize(StaticRoles = "Administrators")]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage AdminCancelBooking([FromBody] CancelBookingRequest cancelRequest)
        {
            if (!IsServiceAvailable(out var errorResponse)) return errorResponse;

            if (cancelRequest == null || cancelRequest.BookingId <= 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid cancellation request.");
            }

            try
            {
                BookingService.CancelBooking(cancelRequest.BookingId);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = "Booking cancelled by admin." });
            }
            catch (Exception ex)
            {
                DotNetNuke.Services.Exceptions.Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, 
                    "An error occurred during cancellation: " + ex.Message);
            }
        }

        public class CancelBookingRequest
        {
            public int BookingId { get; set; }
        }
    }
}