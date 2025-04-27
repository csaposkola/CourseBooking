using CourseBooking.Services;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Security; // For Requires
using DotNetNuke.Services.Exceptions; // For Exceptions logging

namespace CourseBooking.Controllers.Api
{
    public class CourseApiController : DnnApiController
    {
        private readonly IBookingService _bookingService;

        // Constructor without DI
        public CourseApiController() : this(new BookingService())
        {
        }

        // Constructor for potential DI
        public CourseApiController(IBookingService bookingService)
        {
             _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));

             // REMOVED THE CALL TO TrySetServiceContext() FROM THE CONSTRUCTOR
             // TrySetServiceContext();
        }

        // REMOVED THE TrySetServiceContext() method completely as it's no longer needed or safe here.
        /*
        private void TrySetServiceContext()
        {
             try
             {
                 // ... code removed ...
             }
             catch (Exception ex)
             {
                 Exceptions.LogException(new InvalidOperationException("Failed to set context in CourseApiController.", ex));
             }
        }
        */

        // Refined GetCurrentModuleId - prioritizes ActiveModule (if available at action time), then Headers, then QueryString
        private int GetCurrentModuleId(int explicitModuleId = -1)
        {
            // 1. Use explicit ID if provided and valid
            if (explicitModuleId > 0) return explicitModuleId;

            // 2. Try ActiveModule (Often available when the ACTION executes)
            // Access ActiveModule safely via DnnApiController properties
            if (ActiveModule != null && ActiveModule.ModuleID > 0) return ActiveModule.ModuleID;

            // 3. Try Request Headers (Reliable for AJAX calls if set by client)
            var request = Request; // Get request object safely from the controller property
            if (request?.Headers.Contains("ModuleId") == true) {
                var moduleIdHeader = request.Headers.GetValues("ModuleId").FirstOrDefault();
                if (int.TryParse(moduleIdHeader, out int moduleIdFromHeader) && moduleIdFromHeader > 0) {
                    return moduleIdFromHeader;
                }
            }

            // 4. Try Request Query String (Good for direct GET API calls)
            if (request != null) {
                var queryParams = request.GetQueryNameValuePairs();
                var moduleIdPair = queryParams.FirstOrDefault(p => p.Key.Equals("moduleid", StringComparison.OrdinalIgnoreCase));
                if (moduleIdPair.Key != null && int.TryParse(moduleIdPair.Value, out int mid) && mid > 0) {
                    return mid;
                }
            }

            // 5. Fallback to HttpContext Query String (Less ideal for API)
            var httpContext = System.Web.HttpContext.Current;
            if (httpContext != null)
            {
                var midStr = httpContext.Request.QueryString["moduleid"];
                if (!string.IsNullOrEmpty(midStr) && int.TryParse(midStr, out int mid) && mid > 0)
                {
                    return mid;
                }
            }

            // Could not determine ModuleId - Log the failure
            Exceptions.LogException(new InvalidOperationException("CourseApiController could not determine ModuleId from context, headers, or parameters."));
            return -1;
        }


        // Check service instance exists.
        private bool IsServiceAvailable(out HttpResponseMessage errorResponse)
        {
            if (_bookingService == null)
            {
                errorResponse = Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "Booking service is unavailable."); // Keep message in English for API consistency? Or use localized resource.
                return false;
            }
            errorResponse = null;
            return true;
        }

        [HttpGet]
        [AllowAnonymous] // Keep anonymous unless login is required to see schedules
        public HttpResponseMessage GetCourseSchedules(int year, int month, int moduleId = -1)
        {
            if (!IsServiceAvailable(out var serviceErrorResponse))
            {
                return serviceErrorResponse;
            }

            int currentModuleId = GetCurrentModuleId(moduleId); // Use the refined GetCurrentModuleId
            if (currentModuleId <= 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Module context could not be determined. Please provide a valid 'moduleId' parameter or ensure the request includes it correctly.");
            }

            if (year < 1900 || year > 2100 || month < 1 || month > 12)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid year or month specified.");
            }

            try
            {
                var startDate = new DateTime(year, month, 1);
                // Ensure endDate captures the *entire* last day of the month in UTC
                var endDate = startDate.AddMonths(1).AddTicks(-1); // End of the month

                // Call service method with the determined ModuleId
                var schedules = _bookingService.GetCourseSchedules(currentModuleId, startDate.ToUniversalTime(), endDate.ToUniversalTime(), includeInactive: false);

                HashSet<int> userRegisteredScheduleIds = null;
                // Use UserInfo property from DnnApiController
                if (UserInfo != null && UserInfo.UserID > 0)
                {
                    userRegisteredScheduleIds = _bookingService.GetBookingsByUser(UserInfo.UserID, currentModuleId)
                        .Where(b => !b.IsCancelled)
                        .Select(b => b.CourseScheduleID)
                        .ToHashSet();
                }

                var responseSchedules = schedules.Select(s => new
                {
                    s.ID,
                    s.CoursePlanID,
                    StartTime = s.StartTime, // Keep as UTC DateTime for consistency in API response
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
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "An error occurred retrieving course schedules: " + ex.Message);
            }
        }

        // This AdminCancelBooking method requires Edit permissions
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage AdminCancelBooking([FromBody] CancelBookingRequest cancelRequest)
        {
            if (!IsServiceAvailable(out var serviceErrorResponse)) return serviceErrorResponse;

            // Determine ModuleId using the request body value first, then context
            int currentModuleId = GetCurrentModuleId(cancelRequest?.ModuleId ?? -1);
             if (currentModuleId <= 0)
             {
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Module context could not be determined for cancellation.");
             }

            if (cancelRequest == null || cancelRequest.BookingId <= 0)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid cancellation request. Missing BookingId.");
            }

            try
            {
                bool cancelled = _bookingService.CancelBooking(cancelRequest.BookingId, currentModuleId);
                if (cancelled)
                {
                     return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = "Booking cancelled by admin." });
                }
                else
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Booking not found or could not be cancelled (it might already be cancelled or belong to another module instance).");
                }
            }
            catch (BookingException bex)
            {
                 Exceptions.LogException(bex);
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Cancellation failed: " + bex.Message);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred during cancellation: " + ex.Message);
            }
        }

        // Request model for cancellation
        public class CancelBookingRequest
        {
            public int BookingId { get; set; }
            public int ModuleId { get; set; } = -1; // Optional: Client can provide ModuleId context
        }

        // Placeholder for SendReminders if needed
        /*
        [HttpPost]
        [DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage SendReminders([FromBody] ReminderRequest reminderRequest) { ... }

        public class ReminderRequest { ... }
        */
    }
}