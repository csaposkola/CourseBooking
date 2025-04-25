using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Data;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace CourseBooking.Controllers.Api
{
    [DotNetNuke.Web.Api.DnnAuthorizeAttribute]
    [DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute]
    public class CourseApiController : DotNetNuke.Web.Api.DnnApiController
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

        #region Course Plans

        [System.Web.Http.HttpGetAttribute]
        [System.Web.Http.AllowAnonymousAttribute]
        public HttpResponseMessage Plans_List()
        {
            try
            {
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                var plans = BookingService.FindCoursePlans(isAdmin);
                return Request.CreateResponse(HttpStatusCode.OK, plans);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion

        #region Bookings

        [System.Web.Http.HttpGetAttribute]
        public HttpResponseMessage Bookings_List(int year, int month)
        {
            try
            {
                // Create date range for the specified month
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Check if admin or regular user
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;

                IEnumerable<BookingEntity> bookings;
                if (isAdmin)
                {
                    // Admins can see all bookings
                    bookings = BookingService.FindBookingsByDate(startDate, endDate, true);
                }
                else
                {
                    // Regular users can only see their own bookings
                    bookings = BookingService.FindBookingsByUser(UserInfo.UserID, startDate, endDate);
                }

                return Request.CreateResponse(HttpStatusCode.OK, bookings);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [System.Web.Http.HttpGetAttribute]
        public HttpResponseMessage Bookings_Get(int id)
        {
            try
            {
                var booking = BookingService.FindBookingById(id);

                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found");
                }

                // Check authorization
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                bool isOwner = booking.CreatedByUserID == UserInfo.UserID;

                if (!isAdmin && !isOwner)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You don't have permission to view this booking");
                }

                return Request.CreateResponse(HttpStatusCode.OK, booking);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [System.Web.Http.HttpPostAttribute]
        [DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute]
        public HttpResponseMessage Bookings_Create(BookingEntity booking)
        {
            try
            {
                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Invalid booking data");
                }

                // Set the user ID
                booking.CreatedByUserID = UserInfo.UserID;
                booking.CreatedDate = DateTime.UtcNow;

                var newBooking = BookingService.CreateBooking(booking);
                return Request.CreateResponse(HttpStatusCode.OK, newBooking);
            }
            catch (ArgumentException ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [System.Web.Http.HttpPostAttribute]
        [DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute]
        public HttpResponseMessage Bookings_Cancel(int id)
        {
            try
            {
                var booking = BookingService.FindBookingById(id);

                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found");
                }

                // Check authorization
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                bool isOwner = booking.CreatedByUserID == UserInfo.UserID;

                if (!isAdmin && !isOwner)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You don't have permission to cancel this booking");
                }

                bool success = BookingService.CancelBooking(id);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = success });
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion

        #region Participants

        [System.Web.Http.HttpPostAttribute]
        [DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute]
        public HttpResponseMessage Participants_Create(int bookingId, ParticipantEntity participant)
        {
            try
            {
                if (participant == null)
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Invalid participant data");
                }

                var booking = BookingService.FindBookingById(bookingId);

                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found");
                }

                // Check authorization
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                bool isOwner = booking.CreatedByUserID == UserInfo.UserID;

                if (!isAdmin && !isOwner)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You don't have permission to add participants to this booking");
                }

                // Set the user ID
                participant.AddedByUserID = UserInfo.UserID;

                var newParticipant = BookingService.AddParticipantToBooking(bookingId, participant);
                return Request.CreateResponse(HttpStatusCode.OK, newParticipant);
            }
            catch (ArgumentException ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [System.Web.Http.HttpGetAttribute]
        public HttpResponseMessage Participants_List(int bookingId)
        {
            try
            {
                var booking = BookingService.FindBookingById(bookingId);

                if (booking == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Booking not found");
                }

                // Check authorization
                bool isAdmin = UserInfo.IsInRole("Administrators") || UserInfo.IsSuperUser;
                bool isOwner = booking.CreatedByUserID == UserInfo.UserID;

                if (!isAdmin && !isOwner)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "You don't have permission to view participants for this booking");
                }

                using (var ctx = DataContext.Instance())
                {
                    var participants = ctx.GetRepository<ParticipantEntity>()
                        .Find("WHERE BookingID = @0", bookingId);
                    return Request.CreateResponse(HttpStatusCode.OK, participants);
                }
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        [System.Web.Http.HttpPostAttribute]
        [DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute]
        public HttpResponseMessage UpdateParticipantStatus(int participantId, string status)
        {
            try
            {
                // Only admins can update participant status
                if (!UserInfo.IsInRole("Administrators") && !UserInfo.IsSuperUser)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "Only administrators can update attendance status");
                }

                bool success = BookingService.UpdateParticipantStatus(participantId, status);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = success });
            }
            catch (ArgumentException ex)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion

        #region Notifications

        [System.Web.Http.HttpPostAttribute]
        [DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute]
        public HttpResponseMessage SendReminder(int bookingId, int hoursBeforeCourse)
        {
            try
            {
                // Only admins can send reminders
                if (!UserInfo.IsInRole("Administrators") && !UserInfo.IsSuperUser)
                {
                    return Request.CreateResponse(HttpStatusCode.Forbidden, "Only administrators can send reminders");
                }

                bool success = BookingService.SendCourseReminder(bookingId, hoursBeforeCourse);
                return Request.CreateResponse(HttpStatusCode.OK, new { Success = success });
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        #endregion
    }
}