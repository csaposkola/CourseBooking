using CourseBooking.Models;
using DotNetNuke.Common;
using DotNetNuke.Data;
using DotNetNuke.Security;
using DotNetNuke.Services.Mail;
using DotNetNuke.Entities.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace CourseBooking.Services
{
    public class BookingService : IBookingService
    {
        private readonly int _moduleId;
        private readonly int _portalId;

        public BookingService(int moduleId, int portalId)
        {
            Requires.NotNegative("moduleId", moduleId);
            Requires.NotNegative("portalId", portalId);

            _moduleId = moduleId;
            _portalId = portalId;
        }

        #region Course Plan Operations

        public IEnumerable<CoursePlanEntity> GetCoursePlans(bool includeNonPublic = false)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var plansRepo = ctx.GetRepository<CoursePlanEntity>();

                if (includeNonPublic)
                {
                    return plansRepo.Get().ToList();
                }
                else
                {
                    return plansRepo.Find("WHERE IsPublic = 1").ToList();
                }
            }
        }

        public CoursePlanEntity GetCoursePlanById(int coursePlanId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                return ctx.GetRepository<CoursePlanEntity>().GetById(coursePlanId);
            }
        }

        #endregion

        #region Course Schedule Operations

        public IEnumerable<CourseScheduleEntity> GetCourseSchedules(DateTime? fromDate = null, DateTime? toDate = null, bool includeInactive = false)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var query = includeInactive ? "WHERE 1=1" : "WHERE IsActive = 1";

                if (fromDate.HasValue)
                {
                    query += " AND StartTime >= @0";
                }

                if (toDate.HasValue)
                {
                    query += " AND StartTime <= @1";
                }

                query += " ORDER BY StartTime ASC";

                var schedules = ctx.GetRepository<CourseScheduleEntity>()
                    .Find(query, fromDate, toDate).ToList();

                // Load related course plans
                var planRepo = ctx.GetRepository<CoursePlanEntity>();
                foreach (var schedule in schedules)
                {
                    schedule.CoursePlan = planRepo.GetById(schedule.CoursePlanID);
                    schedule.BookingCount = GetBookingCountForSchedule(schedule.ID);
                }

                return schedules;
            }
        }

        public CourseScheduleEntity GetCourseScheduleById(int scheduleId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var schedule = ctx.GetRepository<CourseScheduleEntity>().GetById(scheduleId);

                if (schedule != null)
                {
                    // Load related course plan
                    schedule.CoursePlan = ctx.GetRepository<CoursePlanEntity>().GetById(schedule.CoursePlanID);
                    schedule.BookingCount = GetBookingCountForSchedule(schedule.ID);
                }

                return schedule;
            }
        }

        public CourseScheduleEntity CreateCourseSchedule(CourseScheduleEntity schedule)
        {
            Requires.NotNull("schedule", schedule);

            using (IDataContext ctx = DataContext.Instance())
            {
                // Set defaults
                schedule.CreatedDate = DateTime.UtcNow;
                if (schedule.AvailableSeats <= 0)
                {
                    var plan = ctx.GetRepository<CoursePlanEntity>().GetById(schedule.CoursePlanID);
                    if (plan != null)
                    {
                        schedule.AvailableSeats = plan.MaxCapacity;
                    }
                    else
                    {
                        schedule.AvailableSeats = 10; // Default value
                    }
                }

                ctx.GetRepository<CourseScheduleEntity>().Insert(schedule);
                return schedule;
            }
        }

        public bool UpdateCourseSchedule(CourseScheduleEntity schedule)
        {
            Requires.NotNull("schedule", schedule);

            using (IDataContext ctx = DataContext.Instance())
            {
                try
                {
                    ctx.GetRepository<CourseScheduleEntity>().Update(schedule);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool DeleteCourseSchedule(int scheduleId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                try
                {
                    var schedule = ctx.GetRepository<CourseScheduleEntity>().GetById(scheduleId);
                    if (schedule != null)
                    {
                        // Instead of deleting, mark as inactive
                        schedule.IsActive = false;
                        ctx.GetRepository<CourseScheduleEntity>().Update(schedule);
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region Booking Operations

        public IEnumerable<BookingEntity> GetBookingsByUser(int userId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var bookings = ctx.GetRepository<BookingEntity>()
                    .Find("WHERE UserID = @0 ORDER BY BookingTime DESC", userId).ToList();

                // Load related schedules and course plans
                LoadBookingDetails(ctx, bookings);

                return bookings;
            }
        }

        public IEnumerable<BookingEntity> GetBookingsByCourseSchedule(int scheduleId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var bookings = ctx.GetRepository<BookingEntity>()
                    .Find("WHERE CourseScheduleID = @0 ORDER BY BookingTime", scheduleId).ToList();

                // Load related schedules and course plans
                LoadBookingDetails(ctx, bookings);

                return bookings;
            }
        }

        public BookingEntity GetBookingById(int bookingId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var booking = ctx.GetRepository<BookingEntity>().GetById(bookingId);

                if (booking != null)
                {
                    // Load related schedule and plan
                    var schedules = new List<BookingEntity> { booking };
                    LoadBookingDetails(ctx, schedules);
                }

                return booking;
            }
        }

        public BookingEntity CreateBooking(int courseScheduleId, int userId, string notes = null)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                // Check if user is already registered
                if (IsUserRegisteredForSchedule(courseScheduleId, userId))
                {
                    throw new InvalidOperationException("You are already registered for this course");
                }

                // Check if there are available seats
                if (!HasScheduleAvailableSeats(courseScheduleId))
                {
                    throw new InvalidOperationException("This course has no available seats");
                }

                // Create booking
                var booking = new BookingEntity
                {
                    CourseScheduleID = courseScheduleId,
                    UserID = userId,
                    BookingTime = DateTime.UtcNow,
                    IsCancelled = false,
                    VoucherCode = GenerateVoucherCode(),
                    PaymentStatus = "Pending",
                    Notes = notes
                };

                ctx.GetRepository<BookingEntity>().Insert(booking);

                // Create participant record
                var participant = new ParticipantEntity
                {
                    BookingID = booking.ID,
                    AttendanceStatus = "Registered"
                };

                ctx.GetRepository<ParticipantEntity>().Insert(participant);

                return booking;
            }
        }

        public bool CancelBooking(int bookingId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var booking = ctx.GetRepository<BookingEntity>().GetById(bookingId);

                if (booking == null)
                {
                    return false;
                }

                // Check if we can cancel (e.g., not too close to start time)
                var schedule = ctx.GetRepository<CourseScheduleEntity>().GetById(booking.CourseScheduleID);
                if (schedule != null && schedule.StartTime < DateTime.UtcNow.AddHours(24))
                {
                    throw new InvalidOperationException("Bookings cannot be cancelled less than 24 hours before the course starts");
                }

                // Update booking status
                booking.IsCancelled = true;
                ctx.GetRepository<BookingEntity>().Update(booking);

                return true;
            }
        }

        #endregion

        #region Notification Operations

        public bool SendBookingConfirmation(int bookingId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var booking = GetBookingById(bookingId);
                if (booking == null)
                {
                    return false;
                }

                var user = UserController.Instance.GetUserById(_portalId, booking.UserID);
                if (user == null)
                {
                    return false;
                }

                // Create email content
                string subject = $"Course Booking Confirmation: {booking.CourseSchedule.CoursePlan.Name}";
                string body = $"Dear {user.DisplayName},\n\n" +
                             $"Your booking for {booking.CourseSchedule.CoursePlan.Name} on {booking.CourseSchedule.StartTime.ToLocalTime():g} has been confirmed.\n" +
                             $"Your voucher code is: {booking.VoucherCode}\n\n" +
                             "Thank you for your registration.";

                // Send email
                string mailResult = Mail.SendMail(
                    "noreply@coursebooking.com",
                    user.Email,
                    "", // CC
                    subject,
                    body,
                    "", // Attachment
                    "", // Attachment name
                    "", // Attachment content type
                    "", // Reply to
                    "", // Reply to display name
                    "HTML" // Email format
                );
                bool emailSent = string.IsNullOrEmpty(mailResult);

                // Record notification
                var notification = new NotificationEntity
                {
                    BookingID = bookingId,
                    NotificationType = "BookingConfirmation",
                    Recipients = user.Email,
                    SentDate = DateTime.UtcNow,
                    TemplateUsed = "BookingConfirmation",
                    IsDeliverySuccessful = emailSent,
                    ErrorLog = emailSent ? null : mailResult
                };

                ctx.GetRepository<NotificationEntity>().Insert(notification);

                // Update booking with voucher sent date
                booking.VoucherSentDate = DateTime.UtcNow;
                ctx.GetRepository<BookingEntity>().Update(booking);

                return true;
            }
        }

        public bool SendCourseReminder(int bookingId, int hoursBeforeCourse)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var booking = GetBookingById(bookingId);
                if (booking == null || booking.IsCancelled)
                {
                    return false;
                }

                var user = UserController.Instance.GetUserById(_portalId, booking.UserID);
                if (user == null)
                {
                    return false;
                }

                // Create email content
                string subject = $"Reminder: {booking.CourseSchedule.CoursePlan.Name} - {hoursBeforeCourse} hours to go!";
                string body = $"Dear {user.DisplayName},\n\n" +
                             $"This is a reminder that your course {booking.CourseSchedule.CoursePlan.Name} starts in {hoursBeforeCourse} hours.\n" +
                             $"Date and time: {booking.CourseSchedule.StartTime.ToLocalTime():g}\n" +
                             $"Voucher code: {booking.VoucherCode}\n\n" +
                             "We look forward to seeing you!";

                // Send email
                string mailResult = Mail.SendMail(
                    "noreply@coursebooking.com",
                    user.Email,
                    "", // CC
                    subject,
                    body,
                    "", // Attachment
                    "", // Attachment name
                    "", // Attachment content type
                    "", // Reply to
                    "", // Reply to display name
                    "HTML" // Email format
                );
                bool emailSent = string.IsNullOrEmpty(mailResult);

                // Record notification
                var notification = new NotificationEntity
                {
                    BookingID = bookingId,
                    NotificationType = "CourseReminder",
                    Recipients = user.Email,
                    SentDate = DateTime.UtcNow,
                    TemplateUsed = "CourseReminder",
                    IsDeliverySuccessful = emailSent,
                    ErrorLog = emailSent ? null : mailResult
                };

                ctx.GetRepository<NotificationEntity>().Insert(notification);

                return true;
            }
        }

        #endregion

        #region Helper Methods

        public int GetBookingCountForSchedule(int scheduleId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                return ctx.GetRepository<BookingEntity>()
                    .Find("WHERE CourseScheduleID = @0 AND IsCancelled = 0", scheduleId).Count();
            }
        }

        public bool IsUserRegisteredForSchedule(int scheduleId, int userId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                return ctx.GetRepository<BookingEntity>()
                    .Find("WHERE CourseScheduleID = @0 AND UserID = @1 AND IsCancelled = 0",
                        scheduleId, userId).Any();
            }
        }

        public bool HasScheduleAvailableSeats(int scheduleId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var schedule = ctx.GetRepository<CourseScheduleEntity>().GetById(scheduleId);
                if (schedule == null || !schedule.IsActive)
                {
                    return false;
                }

                int bookingCount = GetBookingCountForSchedule(scheduleId);
                return bookingCount < schedule.AvailableSeats;
            }
        }

        private void LoadBookingDetails(IDataContext ctx, List<BookingEntity> bookings)
        {
            if (bookings == null || !bookings.Any())
            {
                return;
            }

            var scheduleRepo = ctx.GetRepository<CourseScheduleEntity>();
            var planRepo = ctx.GetRepository<CoursePlanEntity>();

            foreach (var booking in bookings)
            {
                // Load schedule
                booking.CourseSchedule = scheduleRepo.GetById(booking.CourseScheduleID);

                if (booking.CourseSchedule != null)
                {
                    // Load course plan
                    booking.CourseSchedule.CoursePlan = planRepo.GetById(booking.CourseSchedule.CoursePlanID);
                }

                // Load user info
                var user = UserController.Instance.GetUserById(_portalId, booking.UserID);
                if (user != null)
                {
                    booking.UserDisplayName = user.DisplayName;
                    booking.UserEmail = user.Email;
                }
            }
        }

        private string GenerateVoucherCode()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[8];
                rng.GetBytes(bytes);
                return "CRS-" + BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8);
            }
        }

        #endregion
    }
}