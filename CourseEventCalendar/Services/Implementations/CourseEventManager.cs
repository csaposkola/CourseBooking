using DotNetNuke.Common.Utilities;
using DotNetNuke.Data;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using CourseEventCalendar.CourseEventCalendar.Models;
using System;
using System.Linq;

namespace CourseEventCalendar.CourseEventCalendar.Services.Implementations
{
    public class CourseEventManager : ICourseEventManager
    {
        public CourseEventManager()
            : this(DotNetNuke.Entities.Users.UserController.Instance)
        { }

        public CourseEventManager(
            IUserController userController
            )
        {
            UserController = userController
                ?? throw new ArgumentNullException(nameof(userController));
        }

        private IUserController UserController { get; }

        private bool HasAccess(CourseEvent courseEvent)
        {
            var currentUser = UserController.GetCurrentUserInfo();
            if (currentUser is null || currentUser.UserID == Null.NullInteger)
                return false;

            return currentUser.IsAdmin
                || currentUser.UserID == courseEvent.CreatedByUserID;
        }

        private void AssertAccess(CourseEvent courseEvent)
        {
            if (!HasAccess(courseEvent))
                throw new CourseEventException("Permission denied.");
        }

        private CourseData[] FetchCourseData(IDataContext ctx, CourseEvent[] events)
        {
            var currentUser = UserController.GetCurrentUserInfo();
            var templateIDs = events.Select(e => e.TemplateID)
                .Distinct()
                .ToArray();

            var templates = templateIDs.Length == 0
                ? new CourseTemplate[0]
                : ctx.GetRepository<CourseTemplate>()
                    .Find("WHERE TemplateID in (@0)", templateIDs)
                    .ToArray();

            var eventIDs = events.Select(e => e.EventID)
                .Distinct()
                .ToArray();
            var participants = eventIDs.Length == 0
                ? new CourseParticipant[0]
                : ctx.GetRepository<CourseParticipant>()
                    .Find("WHERE EventID in (@0)", eventIDs)
                    .ToArray();

            return events
                .Select(e => new CourseData(
                    e,
                    templates.FirstOrDefault(t => t.TemplateID == e.TemplateID),
                    participants.Where(p => p.EventID == e.EventID).ToArray()
                    )
                {
                    User = currentUser.IsAdmin
                        ? UserController.GetUserById(currentUser.PortalID, e.CreatedByUserID)
                        : (e.CreatedByUserID == currentUser.UserID ? currentUser : null)
                })
                .ToArray();
        }

        public CourseParticipant AddParticipantTo(
            int eventID,
            CourseParticipant participant
            )
        {
            var currentUser = UserController.GetCurrentUserInfo();
            if (currentUser.UserID == Null.NullInteger)
                throw new CourseEventException("Guests can't create course events.");

            var courseEvent = FindEventByID(eventID);
            if (courseEvent is null)
                throw new ApplicationException("Course event not found.");

            AssertAccess(courseEvent.Event);

            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseParticipant>();

                participant.EventID = courseEvent.Event.EventID;
                participant.CreatedByUserID = currentUser.UserID;
                participant.CreatedOnDate = DateTime.Now;
                r.Insert(participant);

                return participant;
            }
        }

        public void CancelEvent(int eventID)
        {
            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseEvent>();
                var courseEvent = r.GetById(eventID);
                if (courseEvent != null)
                {
                    AssertAccess(courseEvent);
                    courseEvent.IsCancelled = true;
                    r.Update(courseEvent);
                }
            }
        }

        public CourseEvent CreateEvent(CourseEvent courseEvent)
        {
            var template = FindTemplateByID(courseEvent.TemplateID);

            var currentUser = UserController.GetCurrentUserInfo();
            if (currentUser.UserID == Null.NullInteger)
                throw new CourseEventException("Guests can't create course events.");

            var existingEvents = FindEventsByDate(
                courseEvent.StartAt,
                courseEvent.StartAt.AddHours(template.TotalDuration),
                false
                );
            if (existingEvents.Length != 0)
                throw new ApplicationException("There is another event in the selected time slot.");

            courseEvent.Duration = template.TotalDuration;
            courseEvent.CreatedByUserID = currentUser.UserID;
            courseEvent.CreatedOnDate = DateTime.Now;
            courseEvent.MaxParticipants = template.MaxParticipants; // Set max participants from template
            courseEvent.CurrentParticipants = 0; // Initialize current participants to 0

            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseEvent>();
                r.Insert(courseEvent);
            }

            return courseEvent;
        }

        public CourseData FindEventByID(int eventID)
        {
            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseEvent>();
                var courseEvent = r.GetById(eventID);
                if (courseEvent is null || !HasAccess(courseEvent))
                    return null;

                var template = ctx.GetRepository<CourseTemplate>()
                    .GetById(courseEvent.TemplateID);

                var participants = ctx.GetRepository<CourseParticipant>()
                    .Find("WHERE EventID = @0", eventID)
                    .ToArray();

                return new CourseData(courseEvent, template, participants);
            }
        }

        public CourseTemplate FindTemplateByID(int templateID)
        {
            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseTemplate>();
                return r.GetById(templateID);
            }
        }

        public CourseData[] FindEventsByDate(DateTime? from, DateTime? to, bool findAll)
        {
            using (var ctx = DataContext.Instance())
            {
                var actualFrom = from ?? DateTime.MinValue;
                var actualTo = to ?? DateTime.MaxValue;

                var result = ctx.GetRepository<CourseEvent>()
                    .Find(
                        "WHERE @0 < StartAt AND StartAt < @1 AND (IsCancelled = 0 OR @2 = 1)",
                        actualFrom,
                        actualTo,
                        findAll
                        )
                    .ToArray();
                return FetchCourseData(ctx, result);
            }
        }

        public CourseData[] FindEventsByUser(int userID, DateTime? from, DateTime? to)
        {
            using (var ctx = DataContext.Instance())
            {
                var actualFrom = from ?? DateTime.MinValue;
                var actualTo = to ?? DateTime.MaxValue;

                var result = ctx.GetRepository<CourseEvent>()
                    .Find(
                        "WHERE @0 <= StartAt AND StartAt <= @1 AND CreatedByUserID = @2",
                        actualFrom,
                        actualTo,
                        userID
                        )
                    .ToArray();
                return FetchCourseData(ctx, result);
            }
        }

        public CourseTemplate[] FindCourseTemplates(bool findAll)
        {
            using (var ctx = DataContext.Instance())
            {
                return ctx.GetRepository<CourseTemplate>()
                    .Find("WHERE IsPublic = 1 OR @0 = 1", findAll)
                    .ToArray();
            }
        }

        public bool IsSlotAvailable(DateTime from, int duration)
        {
            using (var ctx = DataContext.Instance())
            {
                var to = from.AddHours(duration);

                var results = ctx.GetRepository<CourseEvent>()
                    .Find(
                        "WHERE @0 <= DATEADD(HOUR, Duration, StartAt) AND StartAt <= @1 AND IsCancelled = 0",
                        from,
                        to
                        )
                    .ToArray();

                return results.Length == 0;
            }
        }

        // New methods for booking functionality
        public bool HasAvailableSeats(int eventId)
        {
            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseEvent>();
                var courseEvent = r.GetById(eventId);

                if (courseEvent == null || courseEvent.IsCancelled)
                    return false;

                return courseEvent.CurrentParticipants < courseEvent.MaxParticipants;
            }
        }

        public int GetAvailableSeats(int eventId)
        {
            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseEvent>();
                var courseEvent = r.GetById(eventId);

                if (courseEvent == null || courseEvent.IsCancelled)
                    return 0;

                return Math.Max(0, courseEvent.MaxParticipants - courseEvent.CurrentParticipants);
            }
        }

        public CourseEvent UpdateEventCapacity(int eventId, int maxParticipants)
        {
            using (var ctx = DataContext.Instance())
            {
                var r = ctx.GetRepository<CourseEvent>();
                var courseEvent = r.GetById(eventId);

                if (courseEvent == null)
                    return null;

                AssertAccess(courseEvent);

                if (maxParticipants < courseEvent.CurrentParticipants)
                    throw new ApplicationException("Cannot reduce capacity below current participant count");

                courseEvent.MaxParticipants = maxParticipants;
                r.Update(courseEvent);

                return courseEvent;
            }
        }
    }
}