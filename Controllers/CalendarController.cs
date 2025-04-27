using System;
using System.Linq;
using System.Web.Mvc;
using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;

namespace CourseBooking.Controllers
{
    [DnnHandleError]
    public class CalendarController : DnnController
    {
        private readonly IBookingService _bookingService;
        
        public CalendarController() {
             if (this.ModuleContext?.Configuration != null && this.PortalSettings != null) {
                  _bookingService = new BookingService(this.ModuleContext.Configuration.ModuleID, this.PortalSettings.PortalId);
             } else {
                  // Log critical failure if context is unavailable during instantiation
                  try {
                      Exceptions.LogException(new InvalidOperationException("CalendarController could not initialize BookingService due to missing Module or Portal context."));
                  } catch { /* Failsafe if logging fails */ }
                  _bookingService = null;
             }
        }

        // Helper to check service availability and set TempData for errors
        private bool EnsureServiceAvailable()
        {
             if (_bookingService == null) {
                  TempData["ErrorMessage"] = "The booking service is currently unavailable. Please try again later.";
                  // Also log this internally if it wasn't logged during construction
                  try {
                      Exceptions.LogException(new InvalidOperationException("BookingService was null when EnsureServiceAvailable was called in CalendarController."));
                  } catch { /* Failsafe */ }
                  return false;
             }
             return true;
        }

        // Action to display the main calendar view
        [ModuleAction(ControlKey = "Edit", TitleKey = "ManageSchedules")]
        public ActionResult Index()
        {
            // Explicitly qualify the static DNN JavaScript class to avoid ambiguity
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);

            // Keep AntiForgery support request for API calls from calendar.js
            ServicesFramework.Instance.RequestAjaxAntiForgerySupport();

            var currentDate = DateTime.Now;
            ViewBag.CurrentYear = currentDate.Year;
            ViewBag.CurrentMonth = currentDate.Month;

            // Pass context info needed by the view/JS
            ViewBag.ModuleId = ModuleContext.Configuration.ModuleID;
            ViewBag.UserId = User?.UserID ?? 0;
            ViewBag.InitialLocale = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;

            // Pass any messages from TempData to ViewBag for the view
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            return View();
        }

        // Action to display details of a specific course schedule
        public ActionResult Details(int id) // id is CourseScheduleID
        {
            if (!EnsureServiceAvailable()) return RedirectToAction("Index");

            var schedule = _bookingService.GetCourseScheduleById(id);
            if (schedule == null)
            {
                TempData["ErrorMessage"] = "The requested course schedule was not found.";
                return RedirectToAction("Index");
            }

            // Determine user's status regarding this schedule
            if (Request.IsAuthenticated)
            {
                ViewBag.IsRegistered = _bookingService.IsUserRegisteredForSchedule(id, User.UserID);
                if ((bool)ViewBag.IsRegistered)
                {
                    // Find the active booking if they are registered
                    ViewBag.UserBooking = _bookingService.GetBookingsByCourseSchedule(id)
                                                    .FirstOrDefault(b => b.UserID == User.UserID && !b.IsCancelled);
                }
                 // Check if registration is possible
                 ViewBag.CanRegister = !(bool)ViewBag.IsRegistered
                                      && schedule.RemainingSeats > 0
                                      && schedule.StartTime.ToUniversalTime() > DateTime.UtcNow;
            }
            else // Anonymous user status
            {
                ViewBag.IsRegistered = false;
                ViewBag.UserBooking = null;
                ViewBag.CanRegister = schedule.RemainingSeats > 0
                                     && schedule.StartTime.ToUniversalTime() > DateTime.UtcNow;
            }

            // Pass messages
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            // Pass the schedule model to the view
            return View(schedule);
        }

        // Action to handle the POST request for registering
        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        [Authorize] // Ensures user is logged in
        public ActionResult Register(int scheduleId, string notes)
        {
            // Check login status (redundant with [Authorize] but safe)
            if (!Request.IsAuthenticated || User.UserID <= 0)
            {
                return new HttpUnauthorizedResult("User must be logged in to register.");
            }

            // Ensure service is ready before proceeding
            if (!EnsureServiceAvailable()) return RedirectToAction("Details", new { id = scheduleId });

            try
            {
                // Attempt booking via service layer
                var booking = _bookingService.CreateBooking(scheduleId, User.UserID, notes);

                // Set success message and redirect to confirmation
                TempData["SuccessMessage"] = "Booking successful!";
                return RedirectToAction("Confirmation", new { id = booking.ID });
            }
            // Catch specific business exceptions from the service
            catch (CourseBooking.Services.BookingException ex) // Qualify exception type if needed
            {
                Exceptions.LogException(ex); // Log the business error details
                TempData["ErrorMessage"] = ex.Message; // Show user-friendly message
                return RedirectToAction("Details", new { id = scheduleId }); // Redirect back to details page
            }
            // Catch any other unexpected errors
            catch (Exception ex)
            {
                Exceptions.LogException(ex); // Log unexpected error
                TempData["ErrorMessage"] = "An unexpected error occurred while processing your booking.";
                return RedirectToAction("Details", new { id = scheduleId }); // Redirect back to details page
            }
        }

        // Action to display the booking confirmation page
        [Authorize] // User must be logged in
        public ActionResult Confirmation(int id) // id is BookingID
        {
            if (!EnsureServiceAvailable()) return RedirectToAction("Index");

            var booking = _bookingService.GetBookingById(id);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking confirmation not found.";
                return RedirectToAction("MyBookings"); // Go to list if specific one isn't found
            }

            // Security: Ensure user owns the booking or is an admin
            if (booking.UserID != User.UserID && !User.IsAdmin)
            {
                 Exceptions.LogException(new UnauthorizedAccessException($"User {User.UserID} attempted to access booking {id} owned by user {booking.UserID}."));
                 TempData["ErrorMessage"] = "You do not have permission to view this booking confirmation.";
                 return RedirectToAction("MyBookings"); // Redirect away
            }

            // Pass messages
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            // Pass booking model to the view
            return View(booking);
        }

        // Action to handle the POST request for cancelling a booking
        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        [Authorize] // User must be logged in
        public ActionResult Cancel(int bookingId)
        {
             if (!EnsureServiceAvailable()) return RedirectToAction("MyBookings");

            // Retrieve booking first to check ownership and existence
            var booking = _bookingService.GetBookingById(bookingId);
             if (booking == null)
             {
                 TempData["ErrorMessage"] = "Booking not found.";
                 return RedirectToAction("MyBookings");
             }
             
             if (booking.UserID != User.UserID && !User.IsAdmin)
             {
                 Exceptions.LogException(new UnauthorizedAccessException($"User {User.UserID} attempted to cancel booking {bookingId} owned by user {booking.UserID}."));
                 TempData["ErrorMessage"] = "You do not have permission to cancel this booking.";
                 return RedirectToAction("MyBookings");
             }

            try
            {
                _bookingService.CancelBooking(bookingId);
                TempData["SuccessMessage"] = "Booking cancelled successfully.";
                return RedirectToAction("MyBookings"); // Show updated list
            }
            // Catch specific business exceptions (e.g., cancellation window closed)
            catch (CourseBooking.Services.BookingException ex) // Qualify if needed
            {
                 Exceptions.LogException(ex); // Log details
                 TempData["ErrorMessage"] = ex.Message; // Show user message
                 return RedirectToAction("MyBookings"); // Redirect back to list
            }

            catch (Exception ex)
            {
                 Exceptions.LogException(ex); // Log details
                 TempData["ErrorMessage"] = "An unexpected error occurred while cancelling the booking.";
                 return RedirectToAction("MyBookings"); // Redirect back to list
            }
        }

        // Action to display the current user's bookings
        [Authorize] // User must be logged in
        public ActionResult MyBookings()
        {
            if (!EnsureServiceAvailable()) return View(Enumerable.Empty<BookingEntity>()); // Show empty view if service fails

            // Check authentication (redundant but safe)
             if (!Request.IsAuthenticated || User.UserID <= 0) {
                 return new HttpUnauthorizedResult();
             }

            // Get user's bookings via service
            var bookings = _bookingService.GetBookingsByUser(User.UserID);

            // Pass messages
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            // Pass bookings model to the view
            return View(bookings);
        }
    }
}