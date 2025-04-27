using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Common;
using DotNetNuke.Entities.Modules; // Required for ModuleInfo
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CourseBooking.Controllers.Api
{
    // AllowAnonymous removed from class level, apply per method if needed
    public class CourseApiController : DnnApiController
    {
        private IBookingService _bookingService;

        // Constructor Dependency Injection (Example - Preferred over ServiceLocator if possible)
        // public CourseApiController(IBookingService bookingService) {
        //     Requires.NotNull("bookingService", bookingService);
        //     _bookingService = bookingService;
        //     // Need to ensure the injected service has context (ModuleId, PortalId)
        //     // This might require custom DI setup in DNN's Startup.cs
        // }

        // Service Property with Context Initialization (Fallback if DI not set up)
        protected IBookingService BookingService
        {
            get
            {
                if (_bookingService == null)
                {
                    try
                    {
                        // Use ServiceLocator pattern (requires BookingService to implement ServiceLocator)
                        // _bookingService = BookingService.Instance;
                        // _bookingService.SetContext(ActiveModule.ModuleID, PortalSettings.PortalId); // Set context explicitly

                        // OR Direct Instantiation:
                        _bookingService = new BookingService(ActiveModule.ModuleID, PortalSettings.PortalId);

                    }
                    catch (Exception ex)
                    {
                        Exceptions.LogException(new InvalidOperationException("Failed to initialize or get context for BookingService in API.", ex));
                        _bookingService = null; // Ensure it's null on failure
                    }
                }
                return _bookingService;
            }
        }

        // Helper to check if service is available
        private bool IsServiceAvailable(out HttpResponseMessage errorResponse)
        {
            if (BookingService == null || ActiveModule == null) // Also check ActiveModule ensure context is valid
            {
                errorResponse = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service or module context is unavailable.");
                return false;
            }
            errorResponse = null;
            return true;
        }

        #region Course Plans (Keep for admin or future reference)

        [HttpGet]
        [DnnAuthorize(StaticRoles = "Administrators")] // Example: Admin only access
        public HttpResponseMessage GetCoursePlans()
        {
            if (!IsServiceAvailable(out var errorResponse)) return errorResponse;

            try
            {
                // Admin check might be redundant due to DnnAuthorize, but safe
                bool canViewAll = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                var plans = BookingService.GetCoursePlans(canViewAll);
                return Request.CreateResponse(HttpStatusCode.OK, plans);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred retrieving course plans.");
            }
        }

        #endregion

        #region Course Schedules (Used by calendar.js)

        [HttpGet]
        [AllowAnonymous] // Calendar view needs this
        public HttpResponseMessage GetCourseSchedules(int year, int month)
        {
            if (!IsServiceAvailable(out var errorResponse)) return errorResponse;

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

                // Create response DTOs to include registration status
                var responseSchedules = schedules.Select(s => new
                {
                    s.ID,
                    s.CoursePlanID,
                    s.StartTime, // Keep as UTC for client-side conversion
                    s.AvailableSeats,
                    s.IsActive, // Should always be true here based on query
                    CoursePlanName = s.CoursePlan?.Name ?? "Unknown Plan", // Send only needed Plan data
                    CoursePlanCategory = s.CoursePlan?.CourseCategory,
                    s.BookingCount,
                    s.RemainingSeats,
                    IsUserRegistered = userRegisteredScheduleIds?.Contains(s.ID) ?? false
                }).ToList();


                return Request.CreateResponse(HttpStatusCode.OK, responseSchedules);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred retrieving course schedules: " + ex.Message);
            }
        }

        #endregion

        [HttpPost]
        [DnnAuthorize(StaticRoles = "Administrators")] // Or appropriate edit role
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
                BookingService.CancelBooking(cancelRequest.BookingId); // Service handles logic and exceptions
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = "Booking cancelled by admin." });
            }
            catch (BookingException bex) // Catch specific errors from service
            {
                 // Logged within service, return user-friendly message
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, bex.Message);
            }
             catch (Exception ex) // Catch unexpected errors
             {
                 Exceptions.LogException(ex); // Already logged in service potentially, but log context here too
                 return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An unexpected error occurred during cancellation.");
             }
        }

        public class CancelBookingRequest
        {
             public int BookingId { get; set; }
        }

        // REMINDER ENDPOINT REMOVED

    }
}