using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using CourseEventCalendar.CourseEventCalendar.Models;
using CourseEventCalendar.CourseEventCalendar.Services;
using CourseEventCalendar.CourseEventCalendar.Util;
using System;
using System.Web.Mvc;
using System.Linq;

namespace CourseEventCalendar.CourseEventCalendar.Controllers
{
    [DnnHandleError]
    public class CourseEventGridController : DnnController
    {
        private readonly ICourseEventManager _courseManager;
        private readonly IBookingService _bookingService;

        public CourseEventGridController()
            : this(
                  new Services.Implementations.CourseEventManager(),
                  new Services.Implementations.BookingService(new Services.Implementations.CourseEventManager()))
        {
        }

        public CourseEventGridController(
            ICourseEventManager courseManager,
            IBookingService bookingService
        )
        {
            _courseManager = courseManager
                             ?? throw new ArgumentNullException(nameof(courseManager));
            _bookingService = bookingService
                             ?? throw new ArgumentNullException(nameof(bookingService));
        }

        private static void InitPopup()
        {
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.DnnPlugins);
            ServicesFramework.Instance.RequestAjaxScriptSupport();
        }

        [AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public ActionResult Index(int? year, int? week)
        {
            var weekOfYear = year.HasValue && week.HasValue
                ? new WeekOfYear(year.Value, week.Value)
                : new WeekOfYear(DateTime.UtcNow);

            var utcNow = DateTime.UtcNow;
            var events = _courseManager.FindEventsByDate(weekOfYear.FirstDay, weekOfYear.LastDay, false);
            ViewBag.Events = events;
            ViewBag.WeekOfYear = weekOfYear;

            return View();
        }

        [AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public ActionResult Create(DateTime? startAt)
        {
            InitPopup();

            var model = new CreateCourseParameters()
            {
                StartAt = startAt ?? DateTime.UtcNow,
            };

            ViewBag.Templates = _courseManager.FindCourseTemplates(
                User.IsAdmin
            );
            ViewBag.ParticipantTypes = new SelectList(new[]
            {
                new SelectListItem() { Text = "-- select --", Value = null, Selected = true },
                new SelectListItem() { Text = "Student", Value = "student", },
                new SelectListItem() { Text = "Instructor", Value = "instructor" },
                new SelectListItem() { Text = "Assistant", Value = "assistant" },
            }, nameof(SelectListItem.Value), nameof(SelectListItem.Text));

            return PartialView("Create", model);
        }

        [HttpGet]
        public ActionResult Detail(int eventID)
        {
            InitPopup(); // Assuming this method exists and is needed

            var courseEvent = _courseManager.FindEventByID(eventID);
            if (courseEvent == null)
            {
                // Handle event not found, perhaps return HttpNotFound() or a specific view
                return HttpNotFound("Event not found");
            }

            // Set default values for ViewBag properties
            ViewBag.UserHasBooking = false;
            ViewBag.UserBookingId = (int?)null; // Use nullable int if needed, or 0/null depending on view logic

            // Check if the user is logged in (UserID > 0 in DNN)
            // Add User null check for safety
            if (User != null && User.UserID > 0)
            {
                // Use User.UserID directly (it's an int)
                var userHasBooking = _bookingService.UserHasBooking(eventID, User.UserID);
                ViewBag.UserHasBooking = userHasBooking;

                if (userHasBooking)
                {
                    // Get the user's bookings
                    // Use User.UserID directly (it's an int)
                    var userBookings = _bookingService.GetUserBookings(User.UserID);

                    // Find the specific booking for this event
                    var booking = userBookings.FirstOrDefault(b => b.EventID == eventID);
                    if (booking != null)
                    {
                        ViewBag.UserBookingId = booking.BookingID;
                    }
                }
            }
            // else: User is not logged in, ViewBag properties keep their default values

            return View(courseEvent);
        }
    }
}