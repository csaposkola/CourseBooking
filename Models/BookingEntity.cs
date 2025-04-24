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
        public int CreatedByUserID { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsCancelled { get; set; }
        public DateTime StartTime { get; set; }
        public int DurationHours { get; set; }
        public int CoursePlanID { get; set; }
        public string VoucherCode { get; set; }
        public DateTime? VoucherSentDate { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentReference { get; set; }

        [IgnoreColumn]
        public CoursePlanEntity CoursePlan { get; set; }

        [IgnoreColumn]
        public DateTime EndTime => StartTime.AddHours(DurationHours);
    }
}