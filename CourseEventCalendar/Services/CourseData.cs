using DotNetNuke.Entities.Users;
using CourseEventCalendar.CourseEventCalendar.Models;
using System;

namespace CourseEventCalendar.CourseEventCalendar.Services
{
    public class CourseData
    {
        public int EventID => Event.EventID;

        public int Duration => Event.Duration;

        public CourseEvent Event { get; }

        public CourseParticipant[] Participants { get; }

        public CourseTemplate Template { get; }

        public UserInfo User { get; internal set; }

        internal CourseData(
            CourseEvent courseEvent,
            CourseTemplate template,
            CourseParticipant[] participants
            )
        {
            Event = courseEvent
                ?? throw new ArgumentNullException(nameof(courseEvent));
            Template = template
                ?? throw new ArgumentNullException(nameof(template));
            Participants = participants
                ?? throw new ArgumentNullException(nameof(participants));
        }

        public bool IsScheduledAt(DateTime dateTime, int duration)
            => Event.IsScheduledAt(dateTime, duration);
    }
}