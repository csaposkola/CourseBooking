using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Security;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using System;
using System.Linq;
using System.Web.Mvc;
using DotNetNuke.Services.Exceptions;

namespace CourseBooking.Controllers
{
    [DotNetNuke.Web.Mvc.Framework.ActionFilters.DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
    [DnnHandleError]
    public class ScheduleAdminController : DnnController
    {
        private readonly IBookingService _bookingService;

        // Constructor Injection is preferred
        public ScheduleAdminController(IBookingService bookingService)
        {
             _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
             // Ensure context is set for operations like Create/Update that might rely on it
             // In Admin context, ModuleContext should be reliably available.
             if (ModuleContext?.Configuration != null && PortalSettings != null)
             {
                 _bookingService.SetContext(ModuleContext.Configuration.ModuleID, PortalSettings.PortalId);
             }
             else
             {
                  Exceptions.LogException(new InvalidOperationException("ScheduleAdminController could not set BookingService context."));
             }
        }

        // Fallback constructor for environments without DI
        public ScheduleAdminController() : this(new BookingService()) {}


        // Helper to get ModuleId safely
        private int GetCurrentModuleId()
        {
            // Admin controllers should reliably have ModuleContext
            if (ModuleContext?.Configuration != null && ModuleContext.Configuration.ModuleID > 0)
            {
                return ModuleContext.Configuration.ModuleID;
            }
            Exceptions.LogException(new InvalidOperationException("Could not determine ModuleId in ScheduleAdminController."));
            return -1; // Indicate error
        }

        [ModuleAction(ControlKey = "Edit", TitleKey = "ManageSchedules")] // Ensure ControlKey matches DNN definition
        public ActionResult Index()
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);
            DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxAntiForgerySupport(); // Keep for potential future AJAX calls from this view

            int moduleId = GetCurrentModuleId();
            if (moduleId <= 0) {
                // Handle error appropriately, maybe show an error message in the view
                ViewBag.ErrorMessage = "Error loading schedule data: Module context unavailable.";
                return View(new CourseScheduleEntity[0]); // Return empty list
            }

            var fromDate = DateTime.UtcNow.Date; // Use Date part for consistency
            var toDate = fromDate.AddMonths(3);

            // Pass ModuleId to the service method
            var schedules = _bookingService.GetCourseSchedules(moduleId, fromDate, toDate, includeInactive: true); // Admin view includes inactive
            return View(schedules);
        }

        public ActionResult Create()
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);

            int moduleId = GetCurrentModuleId();
             if (moduleId <= 0) {
                 // Handle error - cannot create without context
                 TempData["ErrorMessage"] = "Cannot create schedule: Module context unavailable.";
                 return RedirectToAction("Index");
             }

            // Course Plans are global, no moduleId needed here
            ViewBag.CoursePlans = _bookingService.GetCoursePlans(true); // Admin sees all plans
            return View(new CourseScheduleEntity
            {
                StartTime = DateTime.Now.AddDays(1).Date.AddHours(10), // Use local time for default display
                IsActive = true,
                ModuleId = moduleId // Pre-populate ModuleId if needed by model binding, though service sets it on create
            });
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Create(CourseScheduleEntity model)
        {
             int moduleId = GetCurrentModuleId();
             if (moduleId <= 0) {
                 ModelState.AddModelError("", "Error creating schedule: Module context unavailable.");
                 ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                 return View(model);
             }

            // Convert local time from form back to UTC for saving
            if (Request.Form["StartDate"] != null && Request.Form["StartTime"] != null)
            {
                if (DateTime.TryParse(Request.Form["StartDate"] + " " + Request.Form["StartTime"], out DateTime localStartTime))
                {
                    // Assuming the server's local time matches the user's input expectation
                    model.StartTime = localStartTime.ToUniversalTime();
                }
                else
                {
                     ModelState.AddModelError("StartTime", "Invalid date/time format.");
                }
            }


            if (!ModelState.IsValid)
            {
                ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                return View(model);
            }

            try
            {
                // Service context should be set via constructor/property for ModuleId/PortalId
                model.CreatedByUserID = User.UserID;
                // The CreateCourseSchedule method uses the service's internal ModuleId
                var schedule = _bookingService.CreateCourseSchedule(model);
                TempData["SuccessMessage"] = "Schedule created successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                 Exceptions.LogException(ex);
                ModelState.AddModelError("", "Error creating schedule: " + ex.Message);
                ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                return View(model);
            }
        }

        public ActionResult Edit(int id) // id is ScheduleID
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);

            int moduleId = GetCurrentModuleId();
             if (moduleId <= 0) {
                 TempData["ErrorMessage"] = "Error editing schedule: Module context unavailable.";
                 return RedirectToAction("Index");
             }

            // Pass ModuleId to the service method
            var schedule = _bookingService.GetCourseScheduleById(id, moduleId);
            if (schedule == null)
            {
                TempData["ErrorMessage"] = "Schedule not found.";
                return RedirectToAction("Index"); // Use RedirectToAction instead of HttpNotFound() for better user experience in DNN
            }

            ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
            return View(schedule);
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Edit(CourseScheduleEntity model)
        {
            int moduleId = GetCurrentModuleId();
             if (moduleId <= 0) {
                 ModelState.AddModelError("", "Error updating schedule: Module context unavailable.");
                 ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                 return View(model);
             }

            // Convert local time from form back to UTC for saving
            if (Request.Form["StartDate"] != null && Request.Form["StartTime"] != null)
            {
                if (DateTime.TryParse(Request.Form["StartDate"] + " " + Request.Form["StartTime"], out DateTime localStartTime))
                {
                    model.StartTime = localStartTime.ToUniversalTime();
                }
                 else
                {
                     ModelState.AddModelError("StartTime", "Invalid date/time format.");
                }
            }
            
            model.IsActive = Request.Form["IsActive"]?.Split(',').Contains("true", StringComparer.OrdinalIgnoreCase) ?? false;

            if (!ModelState.IsValid)
            {
                ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                // Need to reload booking count if returning to view
                var originalSchedule = _bookingService.GetCourseScheduleById(model.ID, moduleId);
                 model.BookingCount = originalSchedule?.BookingCount ?? 0;
                return View(model);
            }

            try
            {
                 // UpdateCourseSchedule uses internal service ModuleId for ownership check and update
                if (_bookingService.UpdateCourseSchedule(model))
                {
                    TempData["SuccessMessage"] = "Schedule updated successfully.";
                    return RedirectToAction("Index");
                }
                else
                {
                    // This case might occur if Update returns false without an exception
                    ModelState.AddModelError("", "Failed to update course schedule. Please try again.");
                    ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                    model.BookingCount = _bookingService.GetBookingCountForSchedule(model.ID, moduleId); // Reload count
                    return View(model);
                }
            }
            catch (BookingException bex) // Catch specific business rule exceptions
            {
                 Exceptions.LogException(bex);
                 ModelState.AddModelError("", "Error updating schedule: " + bex.Message);
                 ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                 model.BookingCount = _bookingService.GetBookingCountForSchedule(model.ID, moduleId); // Reload count
                 return View(model);
            }
            catch (Exception ex) // Catch other unexpected exceptions
            {
                 Exceptions.LogException(ex);
                 ModelState.AddModelError("", "An unexpected error occurred while updating schedule: " + ex.Message);
                 ViewBag.CoursePlans = _bookingService.GetCoursePlans(true);
                 model.BookingCount = _bookingService.GetBookingCountForSchedule(model.ID, moduleId); // Reload count
                 return View(model);
            }
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Delete(int id) // id is ScheduleID
        {
             int moduleId = GetCurrentModuleId();
             if (moduleId <= 0) {
                 TempData["ErrorMessage"] = "Error deleting schedule: Module context unavailable.";
                 return RedirectToAction("Index");
             }

            try
            {
                // Pass ModuleId to the service method
                _bookingService.DeleteCourseSchedule(id, moduleId);
                TempData["SuccessMessage"] = "Schedule marked as inactive."; // Changed from "deleted"
                return RedirectToAction("Index");
            }
            catch (BookingException bex)
            {
                Exceptions.LogException(bex);
                TempData["ErrorMessage"] = "Error deleting schedule: " + bex.Message;
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                 Exceptions.LogException(ex);
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting schedule: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // View Bookings for a specific Schedule
        public ActionResult Bookings(int id) // id is ScheduleID
        {
             int moduleId = GetCurrentModuleId();
             if (moduleId <= 0) {
                 TempData["ErrorMessage"] = "Error viewing bookings: Module context unavailable.";
                 return RedirectToAction("Index");
             }

            // Pass ModuleId to get the schedule
            var schedule = _bookingService.GetCourseScheduleById(id, moduleId);
            if (schedule == null)
            {
                 TempData["ErrorMessage"] = "Schedule not found.";
                 return RedirectToAction("Index");
            }

            // Pass ModuleId to get the bookings associated with this schedule
            var bookings = _bookingService.GetBookingsByCourseSchedule(id, moduleId);
            ViewBag.Schedule = schedule; // Pass schedule to view for context
            return View(bookings);
        }
    }
}