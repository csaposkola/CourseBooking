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
using DotNetNuke.Security.Permissions;

namespace CourseBooking.Controllers
{
    [DnnHandleError]
    public class CalendarController : DnnController
    {
        private readonly IBookingService _bookingService;

        // Constructor without DI (simple instantiation)
        public CalendarController() : this(new BookingService())
        {
        }

        // Constructor for potential DI or direct instantiation
        public CalendarController(IBookingService bookingService)
        {
             _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
             // Context setting removed from constructor
        }

        // Helper to get ModuleId reliably (includes header check)
        private int GetCurrentModuleId()
        {
            // Prioritize ModuleContext if available (might be null early in POST)
            if (ModuleContext?.Configuration != null && ModuleContext.Configuration.ModuleID > 0)
            {
                return ModuleContext.Configuration.ModuleID;
            }

            // Try ActiveModule (might also be null early in POST)
            if (ActiveModule != null && ActiveModule.ModuleID > 0)
            {
                return ActiveModule.ModuleID;
            }

            // Check Request Headers (Set by ajaxHandler.js for POST/AJAX - might still be useful elsewhere)
            if (Request?.Headers["ModuleId"] != null && int.TryParse(Request.Headers["ModuleId"], out int moduleIdFromHeader) && moduleIdFromHeader > 0)
            {
                return moduleIdFromHeader;
            }

            // Fallback to trying QueryString (less reliable for MVC POST Actions, but good for GET)
            if (Request?.QueryString["moduleId"] != null && int.TryParse(Request.QueryString["moduleId"], out int moduleIdFromQs))
            {
                if (moduleIdFromQs > 0) return moduleIdFromQs;
            }

            // Log failure if no valid ModuleId found
            Exceptions.LogException(new InvalidOperationException($"Failed to get ModuleId in CalendarController. Context/ActiveModule null, Header missing/invalid, QueryString missing/invalid. User: {User?.UserID ?? -1}"));
            return -1; // Indicate failure
        }

        // Helper to check service availability
        private bool EnsureServiceAvailable()
        {
             if (_bookingService == null) {
                  TempData["ErrorMessage"] = "A foglalási szolgáltatás jelenleg nem elérhető. Kérjük, próbálja újra később.";
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
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);
            ServicesFramework.Instance.RequestAjaxAntiForgerySupport(); // Keep for potential future use or other forms

            var currentModuleId = GetCurrentModuleId();
            if (currentModuleId <= 0)
            {
                ViewBag.ErrorMessage = "Hiba: A modul kontextus nem határozható meg. A naptár nem jeleníthető meg.";
                Exceptions.LogException(new InvalidOperationException("Failed to get ModuleId in CalendarController.Index."));
                return View();
            }

            var currentDate = DateTime.Now;
            ViewBag.CurrentYear = currentDate.Year;
            ViewBag.CurrentMonth = currentDate.Month;
            ViewBag.ModuleId = currentModuleId;
            ViewBag.UserId = User?.UserID ?? 0;
            ViewBag.IsAdmin = User != null &&
                (User.IsInRole(PortalSettings.AdministratorRoleName) ||
                 ModulePermissionController.HasModulePermission(ModuleContext.Configuration.ModulePermissions, "EDIT"));
            ViewBag.InitialLocale = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;
            // Display messages from previous actions (redirects)
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            return View();
        }

        // Action to display details of a specific course schedule
        public ActionResult Details(int id) // id is CourseScheduleID
        {
            if (!EnsureServiceAvailable()) return RedirectToAction("Index");

            var currentModuleId = GetCurrentModuleId();
            if (currentModuleId <= 0)
            {
                Exceptions.LogException(new InvalidOperationException($"Failed to get ModuleId in CalendarController.Details for Schedule ID {id}."));
                TempData["ErrorMessage"] = "Hiba történt a kurzus részleteinek lekérése közben (Kontextus Hiba).";
                return RedirectToAction("Index");
            }

            var schedule = _bookingService.GetCourseScheduleById(id, currentModuleId);
            if (schedule == null)
            {
                Exceptions.LogException(new Exception($"Schedule not found. Details requested for Schedule ID {id} in Module ID {currentModuleId}. User: {User?.UserID ?? -1}"));
                TempData["ErrorMessage"] = "A kért kurzus időpont nem található, vagy nem tartozik ehhez a modul példányhoz.";
                return RedirectToAction("Index");
            }

            // Determine user's status regarding this schedule
            if (Request.IsAuthenticated)
            {
                ViewBag.IsRegistered = _bookingService.IsUserRegisteredForSchedule(id, User.UserID, currentModuleId);
                if ((bool)ViewBag.IsRegistered)
                {
                    ViewBag.UserBooking = _bookingService.GetBookingsByCourseSchedule(id, currentModuleId)
                                                    .FirstOrDefault(b => b.UserID == User.UserID && !b.IsCancelled);
                }
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

            // Display messages from previous actions (redirects)
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            // We don't need the AJAX check here anymore as the form post is standard
            return View(schedule);
        }

        // Action to handle the POST request for registering (Standard POST)
        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        [Authorize] // Ensures user is logged in
        public ActionResult Register(int scheduleId, string notes) // Make sure form field name is 'scheduleId'
        {
            if (!Request.IsAuthenticated || User.UserID <= 0)
            {
                return new HttpUnauthorizedResult("A foglaláshoz bejelentkezés szükséges.");
            }

            if (!EnsureServiceAvailable()) return RedirectToAction("Details", new { id = scheduleId });

            var currentModuleId = GetCurrentModuleId();
            if (currentModuleId <= 0)
            {
                Exceptions.LogException(new InvalidOperationException($"Failed to get ModuleId within CalendarController.Register action for Schedule ID {scheduleId}. Booking cannot proceed. User: {User?.UserID}"));
                TempData["ErrorMessage"] = "Hiba történt a foglalás feldolgozása közben (Modul Kontextus Hiba). Kérjük, próbálja újra.";
                return RedirectToAction("Details", new { id = scheduleId });
            }

            try
            {
                var booking = _bookingService.CreateBooking(scheduleId, User.UserID, currentModuleId, notes);

                // --- Standard Redirect on Success ---
                TempData["SuccessMessage"] = "Foglalás sikeres!";
                return RedirectToAction("Confirmation", new { id = booking.ID });
            }
            catch (CourseBooking.Services.BookingException ex)
            {
                Exceptions.LogException(ex);
                // --- Redirect back to Details with Error ---
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Details", new { id = scheduleId });
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                Exceptions.LogException(new Exception($"ArgumentOutOfRangeException likely due to invalid ModuleId ({currentModuleId}) in Register action.", argEx));
                // --- Redirect back to Details with Error ---
                TempData["ErrorMessage"] = "Hiba történt a foglalás feldolgozása közben (Érvénytelen Kontextus). Kérjük, próbálja újra.";
                return RedirectToAction("Details", new { id = scheduleId });
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                // --- Redirect back to Details with Error ---
                TempData["ErrorMessage"] = "Váratlan hiba történt a foglalás feldolgozása közben.";
                return RedirectToAction("Details", new { id = scheduleId });
            }
        }

        // Action to display the booking confirmation page
        [Authorize]
        public ActionResult Confirmation(int id) // id is BookingID
        {
            if (!EnsureServiceAvailable()) return RedirectToAction("Index");

             var currentModuleId = GetCurrentModuleId();
             if (currentModuleId <= 0)
             {
                 Exceptions.LogException(new InvalidOperationException($"Failed to get ModuleId in CalendarController.Confirmation for Booking ID {id}."));
                 TempData["ErrorMessage"] = "Hiba történt a foglalás visszaigazolásának lekérése közben (Kontextus Hiba).";
                 return RedirectToAction("MyBookings");
             }

            var booking = _bookingService.GetBookingById(id, currentModuleId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "A foglalási visszaigazolás nem található, vagy nem ehhez a modulhoz tartozik.";
                return RedirectToAction("MyBookings");
            }

            if (booking.UserID != User.UserID && !(User.IsInRole(PortalSettings.AdministratorRoleName) || ModulePermissionController.HasModulePermission(ModuleContext.Configuration.ModulePermissions, "EDIT")))
            {
                 Exceptions.LogException(new UnauthorizedAccessException($"User {User.UserID} attempted to access booking {id} owned by user {booking.UserID} (Module {currentModuleId})."));
                 TempData["ErrorMessage"] = "Nincs jogosultsága megtekinteni ezt a foglalási visszaigazolást.";
                 return RedirectToAction("MyBookings");
            }

            // Display messages from previous actions (like the success message from Register)
            ViewBag.ErrorMessage = TempData["ErrorMessage"];
            ViewBag.SuccessMessage = TempData["SuccessMessage"];

            // We don't need the AJAX check here anymore for this action
            return View(booking);
        }

        // Action to handle the POST request for cancelling a booking (Standard POST)
        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult Cancel(int id) // id is BookingID
        {
            if (!EnsureServiceAvailable()) return RedirectToAction("MyBookings");

            var currentModuleId = GetCurrentModuleId();
            if (currentModuleId <= 0)
            {
                Exceptions.LogException(new InvalidOperationException($"Failed to get ModuleId in CalendarController.Cancel for Booking ID {id}."));
                TempData["ErrorMessage"] = "Hiba történt a foglalás lemondása közben (Kontextus Hiba).";
                // Redirect back to MyBookings as that's the most likely place this was triggered
                return RedirectToAction("MyBookings");
            }

            var booking = _bookingService.GetBookingById(id, currentModuleId);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "A foglalás nem található, vagy nem ehhez a modulhoz tartozik.";
                return RedirectToAction("MyBookings");
            }

            if (booking.UserID != User.UserID && !(User.IsInRole(PortalSettings.AdministratorRoleName) || ModulePermissionController.HasModulePermission(ModuleContext.Configuration.ModulePermissions, "EDIT")))
            {
                Exceptions.LogException(new UnauthorizedAccessException($"User {User.UserID} attempted to cancel booking {id} owned by user {booking.UserID} (Module {currentModuleId})."));
                TempData["ErrorMessage"] = "Nincs jogosultsága lemondani ezt a foglalást.";
                return RedirectToAction("MyBookings");
            }

            try
            {
                _bookingService.CancelBooking(id, currentModuleId);

                // --- Standard Redirect on Success ---
                TempData["SuccessMessage"] = "Foglalás sikeresen lemondva.";
                return RedirectToAction("MyBookings"); // Redirect back to MyBookings after successful cancel
            }
            catch (CourseBooking.Services.BookingException ex)
            {
                 Exceptions.LogException(ex);
                 // --- Redirect back with Error ---
                 TempData["ErrorMessage"] = ex.Message;
                 return RedirectToAction("MyBookings"); // Show error on MyBookings page
            }
            catch (Exception ex)
            {
                 Exceptions.LogException(ex);
                 // --- Redirect back with Error ---
                 TempData["ErrorMessage"] = "Váratlan hiba történt a foglalás lemondása közben.";
                 return RedirectToAction("MyBookings"); // Show error on MyBookings page
            }
        }

        // Action to display the current user's bookings
        [Authorize]
        public ActionResult MyBookings()
        {
            if (!EnsureServiceAvailable()) return View(Enumerable.Empty<BookingEntity>());

             var currentModuleId = GetCurrentModuleId();
             if (currentModuleId <= 0)
             {
                 Exceptions.LogException(new InvalidOperationException($"Failed to get ModuleId in CalendarController.MyBookings. User: {User?.UserID}"));
                 ViewBag.ErrorMessage = "Hiba történt a foglalások lekérése közben (Kontextus Hiba).";
                 // Display the error directly in the view if context fails here
             }

             if (!Request.IsAuthenticated || User.UserID <= 0) {
                 return new HttpUnauthorizedResult();
             }

            // Get user's bookings via service, passing ModuleId
            var bookings = _bookingService.GetBookingsByUser(User.UserID, currentModuleId);

            // Display messages from previous actions (like cancellation results)
             if (ViewBag.ErrorMessage == null) ViewBag.ErrorMessage = TempData["ErrorMessage"]; // Prioritize direct ViewBag error if exists
             ViewBag.SuccessMessage = TempData["SuccessMessage"];

            // We don't need the AJAX check here anymore for this action
            return View(bookings);
        }
    }
}