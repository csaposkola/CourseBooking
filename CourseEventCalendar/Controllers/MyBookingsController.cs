using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using CourseEventCalendar.CourseEventCalendar.Services;
using System;
using System.Web.Mvc;
using DotNetNuke.Entities.Users;
using CourseEventCalendar.CourseEventCalendar.Models;

namespace CourseEventCalendar.CourseEventCalendar.Controllers
{
    [DnnHandleError]
    public class MyBookingsController : DnnController
    {
        private readonly IBookingService _bookingService;

        // Consider using Dependency Injection for IBookingService
        // instead of creating concrete instances here if possible.
        public MyBookingsController()
            : this(
                  new Services.Implementations.BookingService(
                      new Services.Implementations.CourseEventManager())) // Assuming these concrete classes exist
        {
        }

        public MyBookingsController(IBookingService bookingService)
        {
            _bookingService = bookingService
                ?? throw new ArgumentNullException(nameof(bookingService));
        }

        [AcceptVerbs(HttpVerbs.Post | HttpVerbs.Get)]
        public ActionResult Index()
        {
            if (User == null || User.UserID <= 0)
            {
                return View(new Models.Booking[0]);
            }
            
            var bookings = _bookingService.GetUserBookings(User.UserID);

            return View(bookings);
        }
    }
}