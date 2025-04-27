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
        // Pass ModuleId explicitly to ensure context correctness
        IEnumerable<CourseScheduleEntity> GetCourseSchedules(int moduleId, DateTime? fromDate = null, DateTime? toDate = null, bool includeInactive = false);
        CourseScheduleEntity GetCourseScheduleById(int scheduleId, int moduleId); // Added moduleId
        CourseScheduleEntity CreateCourseSchedule(CourseScheduleEntity schedule); // Assumes context is set during creation
        bool UpdateCourseSchedule(CourseScheduleEntity schedule); // Assumes context is set during update
        bool DeleteCourseSchedule(int scheduleId, int moduleId); // Added moduleId

        // Booking operations
        // Pass ModuleId explicitly
        IEnumerable<BookingEntity> GetBookingsByUser(int userId, int moduleId); // Added moduleId
        IEnumerable<BookingEntity> GetBookingsByCourseSchedule(int scheduleId, int moduleId); // Added moduleId
        BookingEntity GetBookingById(int bookingId, int moduleId); // Added moduleId
        BookingEntity CreateBooking(int courseScheduleId, int userId, int moduleId, string notes = null); // Added moduleId
        bool CancelBooking(int bookingId, int moduleId); // Added moduleId

        // Helper operations
        // Pass ModuleId explicitly
        int GetBookingCountForSchedule(int scheduleId, int moduleId); // Added moduleId
        bool IsUserRegisteredForSchedule(int scheduleId, int userId, int moduleId); // Added moduleId

        // Context setting (keep for methods that inherently need it like Create/Update)
        void SetContext(int moduleId, int portalId);
    }
}