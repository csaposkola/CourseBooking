using DotNetNuke.ComponentModel.DataAnnotations;
using System;

namespace CourseEventCalendar.CourseEventCalendar.Models
{
    [TableName("CourseEventCalendar_CourseEvents")]
    [PrimaryKey(nameof(EventID), AutoIncrement = true)]
    public class CourseEvent
    {
        public int EventID { get; set; }

        public int CreatedByUserID { get; set; }

        public DateTime CreatedOnDate { get; set; }

        public bool IsCancelled { get; set; }

        public DateTime StartAt { get; set; }

        public int Duration { get; set; }

        public int TemplateID { get; set; }

        public int MaxParticipants { get; set; } = 10;

        public int CurrentParticipants { get; set; } = 0;

        public bool IsScheduledAt(DateTime dateTime, int duration)
        {
            var periodEnd = dateTime.AddHours(duration);
            var eventEnd = StartAt.AddHours(Duration);
            return StartAt < periodEnd && dateTime < eventEnd;
        }

        [IgnoreColumn]
        public bool HasAvailableSeats => CurrentParticipants < MaxParticipants && !IsCancelled;

        [IgnoreColumn]
        public int AvailableSeats => Math.Max(0, MaxParticipants - CurrentParticipants);
    }
}