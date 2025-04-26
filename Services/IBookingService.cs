using CourseBooking.Models;
using System;
using System.Collections.Generic;

namespace CourseBooking.Services
{
    public interface IBookingService
    {
        // Course plan operations
        IEnumerable<CoursePlanEntity> GetCoursePlans(bool includeNonPublic = false);
        CoursePlanEntity GetCoursePlanById(int coursePlanId);

        // Course schedule operations
        IEnumerable<CourseScheduleEntity> GetCourseSchedules(DateTime? fromDate = null, DateTime? toDate = null, bool includeInactive = false);
        CourseScheduleEntity GetCourseScheduleById(int scheduleId);
        CourseScheduleEntity CreateCourseSchedule(CourseScheduleEntity schedule);
        bool UpdateCourseSchedule(CourseScheduleEntity schedule);
        bool DeleteCourseSchedule(int scheduleId);

        // Booking operations
        IEnumerable<BookingEntity> GetBookingsByUser(int userId);
        IEnumerable<BookingEntity> GetBookingsByCourseSchedule(int scheduleId);
        BookingEntity GetBookingById(int bookingId);
        BookingEntity CreateBooking(int courseScheduleId, int userId, string notes = null);
        bool CancelBooking(int bookingId);

        // Notification operations
        bool SendBookingConfirmation(int bookingId);
        bool SendCourseReminder(int bookingId, int hoursBeforeCourse);

        // Helper operations
        int GetBookingCountForSchedule(int scheduleId);
        bool IsUserRegisteredForSchedule(int scheduleId, int userId);
        bool HasScheduleAvailableSeats(int scheduleId);
    }
}