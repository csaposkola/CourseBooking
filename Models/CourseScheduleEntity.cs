using DotNetNuke.ComponentModel.DataAnnotations;
using System;
using System.Web.Caching;

namespace CourseBooking.Models
{
    [TableName("CourseSchedules")]
    [PrimaryKey(nameof(ID), AutoIncrement = true)]
    [Cacheable("CourseSchedule", CacheItemPriority.Default, 20)]
    [Scope("ModuleId")]
    public class CourseScheduleEntity
    {
        public int ModuleId { get; set; }
        public int ID { get; set; }
        public int CoursePlanID { get; set; }
        public DateTime StartTime { get; set; }
        public int CreatedByUserID { get; set; }
        public DateTime CreatedDate { get; set; }
        public int AvailableSeats { get; set; }
        public bool IsActive { get; set; }

        [IgnoreColumn]
        public CoursePlanEntity CoursePlan { get; set; }

        [IgnoreColumn]
        public DateTime EndTime => CoursePlan != null
            ? StartTime.AddHours(CoursePlan.DurationHours)
            : StartTime;

        [IgnoreColumn]
        public int BookingCount { get; set; }

        [IgnoreColumn]
        public int RemainingSeats => AvailableSeats - BookingCount;
    }
}