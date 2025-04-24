using DotNetNuke.ComponentModel.DataAnnotations;
using System;
using System.Web.Caching;

namespace CourseBooking.Models
{
    [TableName("CourseParticipants")]
    [PrimaryKey(nameof(ID), AutoIncrement = true)]
    [Cacheable("CourseParticipant", CacheItemPriority.Default, 20)]
    [Scope("ModuleId")]
    public class ParticipantEntity
    {
        public int ID { get; set; }
        public int BookingID { get; set; }
        public DateTime AddedDate { get; set; }
        public int AddedByUserID { get; set; }
        public string ParticipantName { get; set; }
        public string Email { get; set; }
        public string AttendanceStatus { get; set; }
        public string Notes { get; set; }

        [IgnoreColumn]
        public BookingEntity Booking { get; set; }
    }
}