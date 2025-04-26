using CourseBooking.Models;
using CourseBooking.Services;
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
                    if (ActiveModule != null)
                    {
                        moduleId = ActiveModule.ModuleID;
                    }
            
                    _bookingService = new BookingService(moduleId, PortalSettings.PortalId);
                }
                return _bookingService;
            }
        }

        #region Course Plans

        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage GetCoursePlans()
        {
            try
            {
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                var plans = BookingService.GetCoursePlans(isAdmin);
                return Request.CreateResponse(HttpStatusCode.OK, plans);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion

        #region Course Schedules

        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage GetCourseSchedules(int year, int month)
        {
            try
            {
                // Create date range for the specified month
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
        
                List<CourseScheduleEntity> schedules = new List<CourseScheduleEntity>();
        
                // Safety check to prevent NullReferenceException
                if (BookingService != null)
                {
                    schedules = BookingService.GetCourseSchedules(startDate, endDate, isAdmin).ToList();

                    // Add user's booking status if authenticated
                    if (UserInfo.UserID > 0)
                    {
                        foreach (var schedule in schedules)
                        {
                            schedule.BookingCount = BookingService.GetBookingCountForSchedule(schedule.ID);
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, schedules);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public HttpResponseMessage GetCourseSchedule(int id)
        {
            try
            {
                var schedule = BookingService.GetCourseScheduleById(id);

                if (schedule == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Schedule not found");
                }

                // Add user's booking status if authenticated
                if (UserInfo.UserID > 0)
                {
                    bool isRegistered = BookingService.IsUserRegisteredForSchedule(id, UserInfo.UserID);
                    // Add a dynamic property - not ideal but works for API response
                    var response = new
                    {
                        Schedule = schedule,
                        IsUserRegistered = isRegistered,
                        CanRegister = !isRegistered && schedule.RemainingSeats > 0 && schedule.StartTime > DateTime.UtcNow
                    };
                    return Request.CreateResponse(HttpStatusCode.OK, response);
                }

                return Request.CreateResponse(HttpStatusCode.OK, schedule);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion

        #region Bookings

        [HttpGet]
        [DnnAuthorize]
        public HttpResponseMessage GetUserBookings()
        {
            try
            {
                if (UserInfo.UserID <= 0)
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, "User not authenticated");
                }

                var bookings = BookingService.GetBookingsByUser(UserInfo.UserID);
                return Request.CreateResponse(HttpStatusCode.OK, bookings);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost]
        [DnnAuthorize]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage RegisterCourse(int scheduleId, string notes = null)
        {
            try
            {
                if (UserInfo.UserID <= 0)
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, "User not authenticated");
                }

                // Create booking
                var booking = BookingService.CreateBooking(scheduleId, UserInfo.UserID, notes);

                // Send confirmation
                BookingService.SendBookingConfirmation(booking.ID);

                return Request.CreateResponse(HttpStatusCode.OK, booking);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        [HttpPost]
        [DnnAuthorize]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage CancelBooking(int bookingId)
        {
            try
            {
                if (UserInfo.UserID <= 0)
                {
                    return Request.CreateResponse(HttpStatusCode.Unauthorized, "User not authenticated");
                }

                var booking = BookingService.GetBookingById(bookingId);

                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found");
                }

                // Check authorization
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                bool isOwner = booking.UserID == UserInfo.UserID;

                if (!isAdmin && !isOwner)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You don't have permission to cancel this booking");
                }

                bool success = BookingService.CancelBooking(bookingId);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = success });
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
        }

        #endregion

        #region Admin Operations

        [HttpGet]
        [DnnAuthorize(StaticRoles = "Administrators")]
        public HttpResponseMessage GetScheduleBookings(int scheduleId)
        {
            try
            {
                var bookings = BookingService.GetBookingsByCourseSchedule(scheduleId);
                return Request.CreateResponse(HttpStatusCode.OK, bookings);
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [HttpPost]
        [DnnAuthorize(StaticRoles = "Administrators")]
        [ValidateAntiForgeryToken]
        public HttpResponseMessage SendReminders(int scheduleId, int hoursBeforeCourse)
        {
            try
            {
                var bookings = BookingService.GetBookingsByCourseSchedule(scheduleId);
                int successCount = 0;

                foreach (var booking in bookings)
                {
                    if (!booking.IsCancelled)
                    {
                        if (BookingService.SendCourseReminder(booking.ID, hoursBeforeCourse))
                        {
                            successCount++;
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    Success = true,
                    Message = $"Sent {successCount} reminders out of {bookings.Count()} bookings"
                });
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion
    }
}