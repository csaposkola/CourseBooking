using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Security;
using DotNetNuke.Web.Api;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using System;
using System.Web.Mvc;

namespace CourseBooking.Controllers
{
    [DotNetNuke.Web.Mvc.Framework.ActionFilters.DnnModuleAuthorize(AccessLevel = SecurityAccessLevel.Edit)]
    [DnnHandleError]
    public class ScheduleAdminController : DnnController
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

        [ModuleAction(ControlKey = "Index", TitleKey = "ManageSchedules")]
        public ActionResult Index()
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);
            DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxAntiForgerySupport();

            var fromDate = DateTime.UtcNow;
            var toDate = fromDate.AddMonths(3);

            var schedules = BookingService.GetCourseSchedules(fromDate, toDate, true);
            return View(schedules);
        }

        public ActionResult Create()
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);

            ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
            return View(new CourseScheduleEntity
            {
                StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
                IsActive = true
            });
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Create(CourseScheduleEntity model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
                return View(model);
            }

            try
            {
                model.CreatedByUserID = User.UserID;
                var schedule = BookingService.CreateCourseSchedule(model);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating schedule: " + ex.Message);
                ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
                return View(model);
            }
        }

        public ActionResult Edit(int id)
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);

            var schedule = BookingService.GetCourseScheduleById(id);
            if (schedule == null)
            {
                return HttpNotFound();
            }

            ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
            return View(schedule);
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Edit(CourseScheduleEntity model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
                return View(model);
            }

            try
            {
                if (BookingService.UpdateCourseSchedule(model))
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to update course schedule");
                    ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating schedule: " + ex.Message);
                ViewBag.CoursePlans = BookingService.GetCoursePlans(true);
                return View(model);
            }
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                BookingService.DeleteCourseSchedule(id);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error deleting schedule: " + ex.Message);
                return RedirectToAction("Index");
            }
        }

        public ActionResult Bookings(int id)
        {
            var schedule = BookingService.GetCourseScheduleById(id);
            if (schedule == null)
            {
                return HttpNotFound();
            }

            var bookings = BookingService.GetBookingsByCourseSchedule(id);
            ViewBag.Schedule = schedule;
            return View(bookings);
        }
    }
}