using CourseEventCalendar.CourseEventCalendar.Models;
using CourseEventCalendar.CourseEventCalendar.Services;
using System;
using System.Net.Http;
using System.Web.Http;

namespace CourseEventCalendar.CourseEventCalendar.Controllers.Api
{
    public class BookingController : CourseApiControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(
            ICourseEventManager courseManager,
            IBookingService bookingService)
            : base(courseManager)
        {
            _bookingService = bookingService
                ?? throw new ArgumentNullException(nameof(bookingService));
        }

        [HttpGet]
        public HttpResponseMessage MyBookings()
        {
            try
            {
                if (UserInfo == null || UserInfo.UserID <= 0)
                    return Json(401, new { error = "You must be logged in to view bookings" });

                // Access UserID directly (no .Value needed for int)
                var bookings = _bookingService.GetUserBookings(UserInfo.UserID);
                return Json(new { bookings });
            }
            catch (Exception ex)
            {
                return JsonException(ex); // Ensure JsonException helper method exists and works
            }
        }

        [HttpGet]
        public HttpResponseMessage EventBookings(int eventId)
        {
            try
            {
                var courseEvent = CourseManager.FindEventByID(eventId);
                if (courseEvent == null)
                    return Json(404, new { error = "Event not found" });

                if (UserInfo == null || !(UserInfo.IsAdmin || courseEvent.Event.CreatedByUserID == UserInfo.UserID))
                    return Json(403, new { error = "You do not have permission to view these bookings" });

                var bookings = _bookingService.GetEventBookings(eventId);
                return Json(new { bookings });
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }

        [HttpPost]
        public HttpResponseMessage Create([FromBody] CreateBookingParameters args)
        {
            try
            {
                if (UserInfo == null || UserInfo.UserID <= 0)
                    return Json(401, new { error = "You must be logged in to make a booking" });

                var booking = _bookingService.CreateBooking(args.EventID, UserInfo.UserID, args.Notes);
                return Json(new { booking });
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }

        [HttpPost]
        public HttpResponseMessage Cancel([FromBody] CancelBookingParameters args)
        {
            try
            {
                if (UserInfo == null || UserInfo.UserID <= 0)
                    return Json(401, new { error = "You must be logged in to cancel a booking" });

                var booking = _bookingService.GetBookingById(args.BookingID);
                if (booking == null)
                    return Json(404, new { error = "Booking not found" });

                if (!(UserInfo.IsAdmin || booking.UserID == UserInfo.UserID))
                    return Json(403, new { error = "You do not have permission to cancel this booking" });

                var result = _bookingService.CancelBooking(args.BookingID);
                return Json(new { success = result });
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }

        private HttpResponseMessage Json(object data)
        {
            return Request.CreateResponse(System.Net.HttpStatusCode.OK, data);
        }

        private HttpResponseMessage Json(System.Net.HttpStatusCode statusCode, object data)
        {
            return Request.CreateResponse(statusCode, data);
        }

        private HttpResponseMessage JsonException(Exception ex)
        {
            
            return Request.CreateResponse(System.Net.HttpStatusCode.InternalServerError, new { error = "An unexpected error occurred." });
        }
    }

    public class CreateBookingParameters
    {
        public int EventID { get; set; }
        public string Notes { get; set; }
    }

    public class CancelBookingParameters
    {
        public int BookingID { get; set; }
    }
}