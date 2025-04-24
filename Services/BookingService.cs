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

        #region Booking Query Operations

        public BookingEntity FindBookingById(int bookingId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var booking = ctx.GetRepository<BookingEntity>().GetById(bookingId);

                if (booking != null)
                {
                    // Load related course plan
                    booking.CoursePlan = ctx.GetRepository<CoursePlanEntity>().GetById(booking.CoursePlanID);
                }

                return booking;
            }
        }

        public IEnumerable<BookingEntity> FindBookingsByUser(int userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var bookingsRepo = ctx.GetRepository<BookingEntity>();
                var query = "WHERE CreatedByUserID = @0 AND IsCancelled = 0";

                if (fromDate.HasValue)
                {
                    query += " AND StartTime >= @1";
                }

                if (toDate.HasValue)
                {
                    query += " AND DATEADD(HOUR, DurationHours, StartTime) <= @2";
                }

                var bookings = bookingsRepo.Find(query, userId, fromDate, toDate).ToList();

                // Load related course plans
                var planRepo = ctx.GetRepository<CoursePlanEntity>();
                foreach (var booking in bookings)
                {
                    booking.CoursePlan = planRepo.GetById(booking.CoursePlanID);
                }

                return bookings;
            }
        }

        public IEnumerable<BookingEntity> FindBookingsByDate(DateTime? fromDate = null, DateTime? toDate = null, bool includeCancelled = false)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var bookingsRepo = ctx.GetRepository<BookingEntity>();
                var query = includeCancelled ? "WHERE 1=1" : "WHERE IsCancelled = 0";

                if (fromDate.HasValue)
                {
                    query += " AND StartTime >= @0";
                }

                if (toDate.HasValue)
                {
                    query += " AND StartTime <= @1";
                }

                var bookings = bookingsRepo.Find(query, fromDate, toDate).ToList();

                // Load related course plans
                var planRepo = ctx.GetRepository<CoursePlanEntity>();
                foreach (var booking in bookings)
                {
                    booking.CoursePlan = planRepo.GetById(booking.CoursePlanID);
                }

                return bookings;
            }
        }

        #endregion

        #region Booking Management Operations

        public BookingEntity CreateBooking(BookingEntity booking)
        {
            Requires.NotNull("booking", booking);

            // Validate time slot availability
            using (IDataContext ctx = DataContext.Instance())
            {
                // Load course plan
                var coursePlan = ctx.GetRepository<CoursePlanEntity>().GetById(booking.CoursePlanID);
                if (coursePlan == null)
                {
                    throw new ArgumentException("Invalid course plan ID");
                }

                // Count existing bookings in this time slot
                var existingBookingsCount = CountExistingBookings(ctx, booking.StartTime, coursePlan.DurationHours);

                // Check if the time slot is available
                if (!IsTimeSlotAvailable(booking.StartTime, coursePlan.DurationHours, coursePlan.MaxCapacity, existingBookingsCount))
                {
                    throw new InvalidOperationException("The selected time slot is not available");
                }

                // Generate voucher code
                booking.VoucherCode = GenerateVoucherCode();

                // Save booking
                ctx.GetRepository<BookingEntity>().Insert(booking);

                return booking;
            }
        }

        public bool IsTimeSlotAvailable(DateTime startTime, int duration, int maxCapacity, int existingBookings)
        {
            // Check minimum advance booking time (24 hours)
            if (startTime < DateTime.UtcNow.AddHours(24))
            {
                return false;
            }

            // Check capacity
            if (existingBookings >= maxCapacity)
            {
                return false;
            }

            return true;
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

                // Update booking status
                booking.IsCancelled = true;
                ctx.GetRepository<BookingEntity>().Update(booking);

                // If cancellation is more than 48 hours before start time, 
                // trigger refund process (not implemented in this example)
                if (booking.StartTime > DateTime.UtcNow.AddHours(48))
                {
                    // Placeholder for refund process
                }

                return true;
            }
        }

        #endregion

        #region Participant Management Operations

        public ParticipantEntity AddParticipantToBooking(int bookingId, ParticipantEntity participant)
        {
            Requires.NotNull("participant", participant);

            using (IDataContext ctx = DataContext.Instance())
            {
                // Get booking
                var booking = ctx.GetRepository<BookingEntity>().GetById(bookingId);
                if (booking == null || booking.IsCancelled)
                {
                    throw new ArgumentException("Invalid or cancelled booking");
                }

                // Get course plan for capacity check
                var coursePlan = ctx.GetRepository<CoursePlanEntity>().GetById(booking.CoursePlanID);

                // Check if there's available capacity
                var participantsCount = ctx.GetRepository<ParticipantEntity>()
                    .Find("WHERE BookingID = @0", bookingId).Count();

                if (participantsCount >= coursePlan.MaxCapacity)
                {
                    throw new InvalidOperationException("The course is at maximum capacity");
                }

                // Validate email
                if (string.IsNullOrEmpty(participant.Email) || !participant.Email.Contains("@"))
                {
                    throw new ArgumentException("Invalid email address");
                }

                // Check for duplicate participants
                bool isDuplicate = ctx.GetRepository<ParticipantEntity>()
                    .Find("WHERE BookingID = @0 AND Email = @1", bookingId, participant.Email).Any();

                if (isDuplicate)
                {
                    throw new InvalidOperationException("This participant is already registered");
                }

                // Set booking ID and defaults
                participant.BookingID = bookingId;
                participant.AddedDate = DateTime.UtcNow;
                participant.AttendanceStatus = "Registered";

                // Save participant
                ctx.GetRepository<ParticipantEntity>().Insert(participant);

                return participant;
            }
        }

        public bool UpdateParticipantStatus(int participantId, string status)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var participant = ctx.GetRepository<ParticipantEntity>().GetById(participantId);

                if (participant == null)
                {
                    return false;
                }

                // Validate status
                if (!IsValidStatus(status))
                {
                    throw new ArgumentException("Invalid attendance status");
                }

                // Update status
                participant.AttendanceStatus = status;
                ctx.GetRepository<ParticipantEntity>().Update(participant);

                return true;
            }
        }

        #endregion

        #region Notification Management Operations

        public bool SendBookingConfirmation(int bookingId)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var booking = FindBookingById(bookingId);
                if (booking == null)
                {
                    return false;
                }

                var participants = ctx.GetRepository<ParticipantEntity>()
                    .Find("WHERE BookingID = @0", bookingId).ToList();

                if (!participants.Any())
                {
                    return false;
                }

                var createdByUser = UserController.Instance.GetUserById(_portalId, booking.CreatedByUserID);

                foreach (var participant in participants)
                {
                    // Create email content
                    string subject = $"Course Booking Confirmation: {booking.CoursePlan.Name}";
                    string body = $"Dear {participant.ParticipantName},\n\n" +
                                 $"Your booking for {booking.CoursePlan.Name} on {booking.StartTime.ToLocalTime():g} has been confirmed.\n" +
                                 $"Your voucher code is: {booking.VoucherCode}\n\n" +
                                 "Thank you for your reservation.";

                    // Mail.SendMail returns string but we need bool
                    string mailResult = Mail.SendMail(
                        createdByUser.Email,
                        participant.Email,
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
                        Recipients = participant.Email,
                        SentDate = DateTime.UtcNow,
                        TemplateUsed = "BookingConfirmation",
                        IsDeliverySuccessful = emailSent,
                        ErrorLog = emailSent ? null : mailResult
                    };

                    ctx.GetRepository<NotificationEntity>().Insert(notification);
                }

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
                var booking = FindBookingById(bookingId);
                if (booking == null || booking.IsCancelled)
                {
                    return false;
                }

                var participants = ctx.GetRepository<ParticipantEntity>()
                    .Find("WHERE BookingID = @0", bookingId).ToList();

                if (!participants.Any())
                {
                    return false;
                }

                var createdByUser = UserController.Instance.GetUserById(_portalId, booking.CreatedByUserID);

                foreach (var participant in participants)
                {
                    // Create email content
                    string subject = $"Reminder: {booking.CoursePlan.Name} - {hoursBeforeCourse} hours to go!";
                    string body = $"Dear {participant.ParticipantName},\n\n" +
                                 $"This is a reminder that your course {booking.CoursePlan.Name} starts in {hoursBeforeCourse} hours.\n" +
                                 $"Date and time: {booking.StartTime.ToLocalTime():g}\n" +
                                 $"Voucher code: {booking.VoucherCode}\n\n" +
                                 "We look forward to seeing you!";

                    // Mail.SendMail returns string but we need bool
                    string mailResult = Mail.SendMail(
                        createdByUser.Email,
                        participant.Email,
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
                        Recipients = participant.Email,
                        SentDate = DateTime.UtcNow,
                        TemplateUsed = "CourseReminder",
                        IsDeliverySuccessful = emailSent,
                        ErrorLog = emailSent ? null : mailResult
                    };

                    ctx.GetRepository<NotificationEntity>().Insert(notification);
                }

                return true;
            }
        }

        public bool SendOrganizersReport(DateTime courseStartTime, string reportType)
        {
            // This is a simplified implementation
            using (IDataContext ctx = DataContext.Instance())
            {
                DateTime fromDate;
                DateTime toDate;

                // Determine date range based on report type
                switch (reportType.ToLower())
                {
                    case "daily":
                        fromDate = courseStartTime.Date;
                        toDate = fromDate.AddDays(1).AddSeconds(-1);
                        break;
                    case "weekly":
                        fromDate = courseStartTime.Date.AddDays(-(int)courseStartTime.DayOfWeek);
                        toDate = fromDate.AddDays(7).AddSeconds(-1);
                        break;
                    case "upcoming":
                        fromDate = DateTime.UtcNow;
                        toDate = fromDate.AddDays(14);
                        break;
                    default:
                        return false;
                }

                // Get courses in the specified range
                var bookings = FindBookingsByDate(fromDate, toDate, false);

                if (!bookings.Any())
                {
                    return false;
                }

                // Create report content
                string subject = $"Course Organizer Report: {reportType}";
                string body = $"Course Report ({reportType})\n\n";

                foreach (var booking in bookings)
                {
                    body += $"- {booking.StartTime.ToLocalTime():g}: {booking.CoursePlan.Name}\n";

                    // Get participants
                    var participants = ctx.GetRepository<ParticipantEntity>()
                        .Find("WHERE BookingID = @0", booking.ID).ToList();

                    body += $"  Participants: {participants.Count} / {booking.CoursePlan.MaxCapacity}\n";

                    foreach (var participant in participants)
                    {
                        body += $"  * {participant.ParticipantName} ({participant.Email})\n";
                    }

                    body += "\n";
                }

                // Send to administrators (simplified)
                var roleController = DotNetNuke.Security.Roles.RoleController.Instance;
                var adminRole = roleController.GetRoleByName(_portalId, "Administrators");
                if (adminRole != null)
                {
                    var adminUsers = DotNetNuke.Security.Roles.RoleController.Instance.GetUsersByRole(_portalId, "Administrators");
                    foreach (var user in adminUsers)
                    {

                        // Mail.SendMail returns string but we need bool
                        string mailResult = Mail.SendMail(
                                "system@coursebooking.com",  // From
                                user.Email,                  // To
                                "",                          // CC
                                subject,
                                body,
                                "",                          // Attachment
                                "",                          // Attachment name
                                "",                          // Attachment content type
                                "",                          // Reply to
                                "",                          // Reply to display name
                                "Text"                       // Email format
                            );
                        bool emailSent = string.IsNullOrEmpty(mailResult);

                        // Record notification
                        var notification = new NotificationEntity
                        {
                            BookingID = 0,  // Not specific to a booking
                            NotificationType = "OrganizerReport",
                            Recipients = user.Email,
                            SentDate = DateTime.UtcNow,
                            TemplateUsed = "OrganizerReport",
                            IsDeliverySuccessful = emailSent,
                            ErrorLog = emailSent ? null : "Failed to send email"
                        };

                        ctx.GetRepository<NotificationEntity>().Insert(notification);
                    }
                }

                return true;
            }
        }

        #endregion

        #region Course Plan Management Operations

        public IEnumerable<CoursePlanEntity> FindCoursePlans(bool includeAll = false)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var plansRepo = ctx.GetRepository<CoursePlanEntity>();

                if (includeAll)
                {
                    return plansRepo.Get().ToList();
                }
                else
                {
                    return plansRepo.Find("WHERE IsPublic = @0", true).ToList();
                }
            }
        }

        #endregion

        #region Helper Methods

        private int CountExistingBookings(IDataContext ctx, DateTime startTime, int duration)
        {
            var endTime = startTime.AddHours(duration);

            // Use the GetRepository method to access the database and count overlapping bookings
            var overlappingBookings = ctx.GetRepository<BookingEntity>().Find(
                @"WHERE IsCancelled = 0 
                AND (
                    (StartTime <= @0 AND DATEADD(HOUR, DurationHours, StartTime) > @0) OR 
                    (StartTime < @1 AND DATEADD(HOUR, DurationHours, StartTime) >= @1) OR 
                    (StartTime >= @0 AND DATEADD(HOUR, DurationHours, StartTime) <= @1)
                )",
                startTime,
                endTime
            );

            return overlappingBookings.Count();
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

        private bool IsValidStatus(string status)
        {
            var validStatuses = new[] { "Registered", "Attended", "NoShow" };
            return validStatuses.Contains(status);
        }

        #endregion
    }
}