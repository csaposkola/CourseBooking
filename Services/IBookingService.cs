using CourseBooking.Models;
using System;
using System.Collections.Generic;

namespace CourseBooking.Services
{
    public interface IBookingService
    {
        // Booking query operations
        BookingEntity FindBookingById(int bookingId);
        IEnumerable<BookingEntity> FindBookingsByUser(int userId, DateTime? fromDate = null, DateTime? toDate = null);
        IEnumerable<BookingEntity> FindBookingsByDate(DateTime? fromDate = null, DateTime? toDate = null, bool includeCancelled = false);

        // Booking management operations
        BookingEntity CreateBooking(BookingEntity booking);
        bool IsTimeSlotAvailable(DateTime startTime, int duration, int maxCapacity, int existingBookings);
        bool CancelBooking(int bookingId);

        // Participant management operations
        ParticipantEntity AddParticipantToBooking(int bookingId, ParticipantEntity participant);
        bool UpdateParticipantStatus(int participantId, string status);

        // Notification management operations
        bool SendBookingConfirmation(int bookingId);
        bool SendCourseReminder(int bookingId, int hoursBeforeCourse);
        bool SendOrganizersReport(DateTime courseStartTime, string reportType);

        // Course plan management operations
        IEnumerable<CoursePlanEntity> FindCoursePlans(bool includeAll = false);
    }
}
