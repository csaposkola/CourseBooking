using DotNetNuke.ComponentModel.DataAnnotations;
using System;
using System.Web.Caching;

namespace CourseBooking.Models
{
    [TableName("CourseBookings")]
    [PrimaryKey(nameof(ID), AutoIncrement = true)]
    [Cacheable("CourseBooking", CacheItemPriority.Default, 20)]
    [Scope("ModuleId")]
    public class BookingEntity
    {
        public int ID { get; set; }
        public int CourseScheduleID { get; set; }
        public int UserID { get; set; }
        public DateTime BookingTime { get; set; }
        public bool IsCancelled { get; set; }
        public string VoucherCode { get; set; }
        public DateTime? VoucherSentDate { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentReference { get; set; }
        public string Notes { get; set; }

        [IgnoreColumn]
        public CourseScheduleEntity CourseSchedule { get; set; }

        [IgnoreColumn]
        public string UserDisplayName { get; set; }

        [IgnoreColumn]
        public string UserEmail { get; set; }
    }
}