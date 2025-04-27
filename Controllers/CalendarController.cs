using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using System;
using System.Linq;
using System.Web.Mvc;

namespace CourseBooking.Controllers
{
    [DnnHandleError]
    public class CalendarController : DnnController
    {
        private IBookingService _bookingService;

        protected IBookingService BookingService
        {
            get
            {
                if (_bookingService == null && ActiveModule != null)
                {
                    _bookingService = new BookingService(ActiveModule.ModuleID, PortalSettings.PortalId);
                }
                return _bookingService;
            }
        }

        [ModuleAction(ControlKey = "Edit", TitleKey = "ManageSchedules")]
        public ActionResult Index()
        {
            // Request required scripts for the calendar view
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);
            DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxAntiForgerySupport();

            // Get current year and month
            var currentDate = DateTime.Now;
            ViewBag.CurrentYear = currentDate.Year;
            ViewBag.CurrentMonth = currentDate.Month;
            ViewBag.IsAdmin = User.IsInRole("Administrators") || User.IsSuperUser;

            return View();
        }

        public ActionResult Details(int id)
        {
            var schedule = BookingService.GetCourseScheduleById(id);
            if (schedule == null)
            {
                return HttpNotFound();
            }

            // Check if user is already registered
            bool isRegistered = false;
            BookingEntity userBooking = null;

            if (User.UserID > 0)
            {
                isRegistered = BookingService.IsUserRegisteredForSchedule(id, User.UserID);
                if (isRegistered)
                {
                    var bookings = BookingService.GetBookingsByCourseSchedule(id);
                    userBooking = bookings.FirstOrDefault(b => b.UserID == User.UserID && !b.IsCancelled);
                }
            }

            ViewBag.IsRegistered = isRegistered;
            ViewBag.UserBooking = userBooking;
            ViewBag.CanRegister = !isRegistered && schedule.RemainingSeats > 0 && schedule.StartTime > DateTime.UtcNow;

            // Handle AJAX requests - with a specific flag to ensure proper layout handling
            bool isAjax = Request.IsAjaxRequest() || Request.QueryString["isAjax"] == "true";
            if (isAjax)
            {
                // For AJAX requests in popups, we should use a specific partial view or adjust the layout
                ViewBag.IsAjaxRequest = true;
                return View(schedule);
            }

            return View(schedule);
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult Register(int id, string notes)
        {
            if (User.UserID <= 0)
            {
                return new HttpUnauthorizedResult();
            }

            try
            {
                var schedule = BookingService.GetCourseScheduleById(id);
                if (schedule == null)
                {
                    return HttpNotFound();
                }

                // Create booking
                var booking = BookingService.CreateBooking(id, User.UserID, notes);

                // Send confirmation
                BookingService.SendBookingConfirmation(booking.ID);

                // For AJAX requests, return JSON result
                if (Request.IsAjaxRequest())
                {
                    return Json(booking);
                }

                return RedirectToAction("Confirmation", new { id = booking.ID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating booking: " + ex.Message);
                
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = ex.Message });
                }
                
                return RedirectToAction("Details", new { id });
            }
        }

        [Authorize]
        public ActionResult Confirmation(int id)
        {
            var booking = BookingService.GetBookingById(id);
            if (booking == null)
            {
                return HttpNotFound();
            }

            // Check authorization
            if (booking.UserID != User.UserID && !User.IsInRole("Administrators") && !User.IsSuperUser)
            {
                return new HttpUnauthorizedResult();
            }

            // Handle AJAX requests - with a specific flag to ensure proper layout handling
            bool isAjax = Request.IsAjaxRequest() || Request.QueryString["isAjax"] == "true";
            if (isAjax)
            {
                ViewBag.IsAjaxRequest = true;
                return View(booking);
            }

            return View(booking);
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        [Authorize]
        public ActionResult Cancel(int id)
        {
            var booking = BookingService.GetBookingById(id);
            if (booking == null)
            {
                return HttpNotFound();
            }

            // Check authorization
            if (booking.UserID != User.UserID && !User.IsInRole("Administrators") && !User.IsSuperUser)
            {
                return new HttpUnauthorizedResult();
            }

            try
            {
                if (BookingService.CancelBooking(id))
                {
                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = true, message = "Booking cancelled successfully" });
                    }
                    return RedirectToAction("MyBookings");
                }
                else
                {
                    if (Request.IsAjaxRequest())
                    {
                        return Json(new { success = false, message = "Failed to cancel booking" });
                    }
                    ModelState.AddModelError("", "Failed to cancel booking.");
                    return RedirectToAction("Confirmation", new { id });
                }
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = false, message = ex.Message });
                }
                ModelState.AddModelError("", "Error: " + ex.Message);
                return RedirectToAction("Confirmation", new { id });
            }
        }

        [Authorize]
        public ActionResult MyBookings()
        {
            var bookings = BookingService.GetBookingsByUser(User.UserID);
            
            // Handle AJAX requests - with a specific flag to ensure proper layout handling
            bool isAjax = Request.IsAjaxRequest() || Request.QueryString["isAjax"] == "true";
            if (isAjax)
            {
                ViewBag.IsAjaxRequest = true;
                return View(bookings);
            }
            
            return View(bookings);
        }
    }
}