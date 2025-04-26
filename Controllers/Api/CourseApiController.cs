using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Common;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;

namespace CourseBooking.Controllers.Api
{
    public class CourseApiController : DnnApiController
    {
        private IBookingService _bookingService;
        protected IBookingService BookingService
        {
            get
            {
                if (_bookingService == null)
                {
                    int moduleId = -1;

                    // 1. Try standard DNN context first
                    if (ActiveModule != null)
                    {
                        moduleId = ActiveModule.ModuleID;
                    }
                    else
                    {
                        try
                        {
                            // Use helper extension method to safely get query parameters
                            var queryString = Request?.GetQueryNameValuePairs(); // Add null check for Request
                            if (queryString != null)
                            {
                                // Look for 'moduleId' case-insensitively
                                var moduleIdPair = queryString.FirstOrDefault(kv => kv.Key.Equals("moduleId", StringComparison.OrdinalIgnoreCase));

                                // If found, try to parse it
                                if (!string.IsNullOrEmpty(moduleIdPair.Value)
                                    && int.TryParse(moduleIdPair.Value, out int parsedModuleId)
                                    && parsedModuleId >= 0) // Ensure it's not negative here too
                                {
                                    moduleId = parsedModuleId; // Assign if valid
                                }
                                else if (!string.IsNullOrEmpty(moduleIdPair.Value))
                                {
                                     // Log if parameter exists but is invalid format
                                     Exceptions.LogException(new ArgumentException($"Invalid moduleId format received in query string: '{moduleIdPair.Value}'", "moduleId"));
                                }
                           }
                        }
                        catch (Exception ex)
                        {
                            // Log potential errors accessing request details, but don't crash
                            Exceptions.LogException(new Exception("Error accessing Request query parameters in BookingService getter.", ex));
                            // moduleId remains -1, letting the BookingService constructor handle the final validation
                        }
                    }

                    // 3. Instantiate the service.
                    // The BookingService constructor should contain the definitive check
                    // for a valid ModuleID (e.g., Requires.NotNegative).
                    try
                    {
                       _bookingService = new BookingService(moduleId, PortalSettings.PortalId);
                    }
                    catch (ArgumentException argEx) // Catch specific exception from Requires.NotNegative
                    {
                        // Log the specific failure reason if instantiation fails
                        Exceptions.LogException(new InvalidOperationException($"Failed to initialize BookingService. {argEx.Message}", argEx));
                        // We cannot proceed without a valid service. Let the calling method handle the null _bookingService.
                        _bookingService = null; // Ensure it stays null on failure
                    }
                    catch (Exception ex) // Catch other potential constructor errors
                    {
                         Exceptions.LogException(new InvalidOperationException("Unexpected error initializing BookingService.", ex));
                         _bookingService = null;
                    }
                }
                return _bookingService;
            }
        }

        // --- API Methods ---

        #region Course Plans

        [HttpGet]
        [AllowAnonymous] // Or [DnnAuthorize] if only logged-in users can see plans
        public HttpResponseMessage GetCoursePlans()
        {
            try
            {
                // Access BookingService safely
                var currentBookingService = BookingService;
                if (currentBookingService == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable. Failed to initialize BookingService.");
                }

                bool isAdmin = UserInfo != null && (UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser);
                var plans = currentBookingService.GetCoursePlans(isAdmin);
                return Request.CreateResponse(HttpStatusCode.OK, plans);
            }
            catch (Exception ex)
            {
                // Log the full exception details
                Exceptions.LogException(ex);
                // Return a generic error message to the client
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while retrieving course plans.");
            }
        }

        #endregion

        #region Course Schedules

        [HttpGet]
        [AllowAnonymous] // Allow anyone to view the schedule
        public HttpResponseMessage GetCourseSchedules(int year, int month) // ModuleID is now handled by the BookingService property getter
        {
            try
            {
                // Validate input parameters
                if (year < 1900 || year > 2100 || month < 1 || month > 12)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid year or month specified.");
                }

                // Access BookingService safely - it now handles ModuleID retrieval
                var currentBookingService = BookingService;
                if (currentBookingService == null)
                {
                    // The getter already logged the initialization error
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable. Failed to initialize BookingService.");
                }

                // Create date range (consider culture info if needed)
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1); // End of the specified month

                bool isAdmin = UserInfo != null && (UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser);

                // Get schedules from the service
                List<CourseScheduleEntity> schedules = currentBookingService.GetCourseSchedules(startDate, endDate, isAdmin)?.ToList() ?? new List<CourseScheduleEntity>();

                // Enhance with booking counts if needed (can be slow if many schedules)
                // Consider if this is better done client-side or on a detail view
                // if (UserInfo != null && UserInfo.UserID > 0) // Check UserInfo nullability
                // {
                //     foreach (var schedule in schedules)
                //     {
                //         if (schedule != null) // Safety check
                //         {
                //            schedule.BookingCount = currentBookingService.GetBookingCountForSchedule(schedule.ID);
                //         }
                //     }
                // }

                return Request.CreateResponse(HttpStatusCode.OK, schedules);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex); // Log the detailed error
                // Send a user-friendly error message back
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while retrieving course schedules. " + ex.Message);
            }
        }

        [HttpGet]
        [AllowAnonymous] // Allow viewing details without login
        public HttpResponseMessage GetCourseSchedule(int id)
        {
             try
             {
                var currentBookingService = BookingService;
                if (currentBookingService == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable.");
                }

                var schedule = currentBookingService.GetCourseScheduleById(id);

                if (schedule == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Schedule not found");
                }

                // Enhance with user-specific data if logged in
                if (UserInfo != null && UserInfo.UserID > 0)
                {
                    bool isRegistered = currentBookingService.IsUserRegisteredForSchedule(id, UserInfo.UserID);
                    int bookingCount = currentBookingService.GetBookingCountForSchedule(id); // Get current booking count
                    schedule.BookingCount = bookingCount; // Update the schedule object if needed elsewhere
                    int remainingSeats = schedule.AvailableSeats - bookingCount;

                    // Create an enhanced response object
                    var response = new
                    {
                        Schedule = schedule,
                        IsUserRegistered = isRegistered,
                        // Simplify CanRegister logic - check if seats available AND hasn't started AND not already registered
                        CanRegister = remainingSeats > 0 && schedule.StartTime > DateTime.UtcNow && !isRegistered,
                        RemainingSeats = remainingSeats
                    };
                    return Request.CreateResponse(HttpStatusCode.OK, response);
                }
                else
                {
                    // For anonymous users, just return the basic schedule
                    // Optionally calculate and include BookingCount/RemainingSeats
                    schedule.BookingCount = currentBookingService.GetBookingCountForSchedule(id);
                    return Request.CreateResponse(HttpStatusCode.OK, new { Schedule = schedule, RemainingSeats = schedule.AvailableSeats - schedule.BookingCount } );
                }
             }
             catch (Exception ex)
             {
                 Exceptions.LogException(ex);
                 return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while retrieving the schedule details.");
             }
        }

        #endregion

        #region Bookings (Requires Authentication)

        [HttpGet]
        [DnnAuthorize] // Ensures user is logged in
        public HttpResponseMessage GetUserBookings()
        {
            try
            {
                // UserInfo is guaranteed non-null by [DnnAuthorize] if properly configured
                if (UserInfo.UserID <= 0) {
                     // Should not happen with DnnAuthorize, but defensive check
                     return Request.CreateResponse(HttpStatusCode.Unauthorized, "Invalid user context.");
                }

                var currentBookingService = BookingService;
                if (currentBookingService == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable.");
                }

                var bookings = currentBookingService.GetBookingsByUser(UserInfo.UserID);
                return Request.CreateResponse(HttpStatusCode.OK, bookings);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while retrieving your bookings.");
            }
        }

        [HttpPost]
        [DnnAuthorize] // Ensures user is logged in
        [ValidateAntiForgeryToken] // Crucial for POST security in DNN Services Framework
        public HttpResponseMessage RegisterCourse([FromBody] CourseRegistrationRequest registrationRequest) // Use FromBody for complex types
        {
             if (registrationRequest == null || registrationRequest.ScheduleId <= 0)
             {
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid registration request data.");
             }

            try
            {
                if (UserInfo.UserID <= 0) {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, "Invalid user context.");
                }

                var currentBookingService = BookingService;
                if (currentBookingService == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable.");
                }

                // Consider adding more validation/checks here (e.g., Can user register? Is course full? Has it started?)
                // These checks might be better placed within the BookingService.CreateBooking method itself.

                // Create booking
                var booking = currentBookingService.CreateBooking(registrationRequest.ScheduleId, UserInfo.UserID, registrationRequest.Notes);

                // Optionally send confirmation (consider doing this asynchronously)
                // currentBookingService.SendBookingConfirmation(booking.ID);

                // Return the created booking details
                return Request.CreateResponse(HttpStatusCode.OK, booking);
            }
            catch (BookingException bex) // Catch specific business logic exceptions from the service
            {
                 Exceptions.LogException(bex); // Log it, but return a user-friendly message
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, bex.Message); // Send specific error back
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex); // Log unexpected errors
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An unexpected error occurred during registration.");
            }
        }

        // Define a simple class for the request body
        public class CourseRegistrationRequest
        {
            public int ScheduleId { get; set; }
            public string Notes { get; set; }
        }


        [HttpPost] // Should likely be DELETE, but POST is common if DELETE isn't configured/allowed easily
        [DnnAuthorize]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage CancelBooking([FromBody] CancelBookingRequest cancelRequest) // Use FromBody
        {
            if (cancelRequest == null || cancelRequest.BookingId <= 0)
            {
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid cancellation request data.");
            }

            try
            {
                 if (UserInfo.UserID <= 0) {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, "Invalid user context.");
                 }

                var currentBookingService = BookingService;
                if (currentBookingService == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable.");
                }

                var booking = currentBookingService.GetBookingById(cancelRequest.BookingId);

                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found.");
                }

                // Authorization Check: Can the current user cancel THIS booking?
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                bool isOwner = booking.UserID == UserInfo.UserID;

                if (!isAdmin && !isOwner)
                {
                    // Log this attempt?
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You do not have permission to cancel this booking.");
                }

                // Add check: Can booking still be cancelled? (e.g., within allowed timeframe before course starts)
                // This logic should ideally be within BookingService.CancelBooking

                // Attempt cancellation
                bool success = currentBookingService.CancelBooking(cancelRequest.BookingId);

                if (success) {
                     return Request.CreateResponse(HttpStatusCode.OK, new { Success = true, Message = "Booking cancelled successfully." });
                } else {
                     // The service layer should ideally throw an exception if cancellation fails for a known reason
                     return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Failed to cancel the booking. It might have already started or cannot be cancelled.");
                }
            }
            catch (BookingException bex) // Catch specific business logic exceptions
            {
                 Exceptions.LogException(bex);
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, bex.Message);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An unexpected error occurred during cancellation.");
            }
        }

        public class CancelBookingRequest
        {
             public int BookingId { get; set; }
        }


        #endregion

        #region Admin Operations (Requires Admin Role)

        // Note: Consider creating a separate AdminApiController for clarity if admin functions grow.

        [HttpGet]
        [DnnAuthorize(StaticRoles = "Administrators")] // Restrict to Administrators static role
        public HttpResponseMessage GetScheduleBookings(int scheduleId)
        {
            if (scheduleId <= 0) {
                 return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid Schedule ID.");
            }
            try
            {
                 var currentBookingService = BookingService;
                 if (currentBookingService == null) { return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable."); }

                var bookings = currentBookingService.GetBookingsByCourseSchedule(scheduleId);
                return Request.CreateResponse(HttpStatusCode.OK, bookings);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while retrieving schedule bookings.");
            }
        }

        [HttpPost]
        [DnnAuthorize(StaticRoles = "Administrators")]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage SendReminders([FromBody] SendRemindersRequest reminderRequest) // Use FromBody
        {
             if (reminderRequest == null || reminderRequest.ScheduleId <= 0 || reminderRequest.HoursBeforeCourse < 0)
             {
                  return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid reminder request data.");
             }

            try
            {
                var currentBookingService = BookingService;
                if (currentBookingService == null) { return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Service unavailable."); }

                // Fetch only relevant bookings (e.g., not cancelled)
                var bookings = currentBookingService.GetBookingsByCourseSchedule(reminderRequest.ScheduleId)
                                                ?.Where(b => !b.IsCancelled) // Filter here or in service
                                                .ToList() ?? new List<BookingEntity>();

                int successCount = 0;
                int failCount = 0;
                List<string> errors = new List<string>();

                foreach (var booking in bookings)
                {
                    try
                    {
                        // The SendCourseReminder should ideally contain logic to check if reminder is applicable/already sent etc.
                        if (currentBookingService.SendCourseReminder(booking.ID, reminderRequest.HoursBeforeCourse))
                        {
                            successCount++;
                        }
                        else
                        {
                           // Maybe log why sending failed if the service indicates it?
                           failCount++;
                        }
                    }
                    catch(Exception mailEx) // Catch errors sending individual emails
                    {
                       failCount++;
                       errors.Add($"Failed sending to Booking ID {booking.ID}: {mailEx.Message}");
                       Exceptions.LogException(new Exception($"Error sending reminder for Booking ID {booking.ID}", mailEx));
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success = failCount == 0, // Overall success if no individual failures
                    Message = $"Attempted to send {successCount + failCount} reminders. Sent successfully: {successCount}. Failed: {failCount}.",
                    Errors = errors // Optionally include individual errors
                });
            }
            catch (Exception ex) // Catch errors fetching bookings etc.
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An unexpected error occurred while processing reminders.");
            }
        }

        public class SendRemindersRequest
        {
            public int ScheduleId { get; set; }
            public int HoursBeforeCourse { get; set; } = 24; // Default value
        }


        #endregion
    }

    // Define custom exception for clearer business logic errors if needed
    public class BookingException : Exception
    {
        public BookingException(string message) : base(message) { }
        public BookingException(string message, Exception innerException) : base(message, innerException) { }
    }
}