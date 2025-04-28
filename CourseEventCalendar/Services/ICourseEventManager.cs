using CourseEventCalendar.CourseEventCalendar.Models;
using System;

namespace CourseEventCalendar.CourseEventCalendar.Services
{
    public interface ICourseEventManager
    {
        CourseData FindEventByID(int eventID);

        CourseData[] FindEventsByUser(
            int userID,
            DateTime? from,
            DateTime? to
            );

        CourseData[] FindEventsByDate(
            DateTime? from,
            DateTime? to,
            bool findAll
            );

        CourseTemplate FindTemplateByID(int templateID);

        CourseEvent CreateEvent(
            CourseEvent courseEvent
            );

        bool IsSlotAvailable(
            DateTime from,
            int duration
            );

        void CancelEvent(
            int eventID
            );

        CourseParticipant AddParticipantTo(
            int eventID,
            CourseParticipant participant
            );

        CourseTemplate[] FindCourseTemplates(bool findAll);

        // New methods for booking functionality
        bool HasAvailableSeats(int eventId);

        int GetAvailableSeats(int eventId);

        CourseEvent UpdateEventCapacity(int eventId, int maxParticipants);
    }
}