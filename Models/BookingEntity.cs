using DotNetNuke.ComponentModel.DataAnnotations;
using System;
using System.Web.Caching;

namespace CourseBooking.Models
{
    [TableName("CourseBookings")]
    [PrimaryKey(nameof(ID), AutoIncrement = true)]
    [Cacheable("CourseBooking", CacheItemPriority.Default, 20)] // Cache settings can be adjusted
    [Scope("ModuleId")] // Ensures data is scoped to the module instance
    public class BookingEntity
    {
        public int ID { get; set; }
        public int CourseScheduleID { get; set; }
        public int UserID { get; set; }
        public DateTime BookingTime { get; set; } // Store as UTC
        public bool IsCancelled { get; set; }
        public string VoucherCode { get; set; }
        // public DateTime? VoucherSentDate { get; set; } // REMOVED - No email sending
        public string PaymentStatus { get; set; }
        public string PaymentReference { get; set; }
        public string Notes { get; set; }

        // These are loaded by the service, not direct DB mapping
        [IgnoreColumn]
        public CourseScheduleEntity CourseSchedule { get; set; }

        [IgnoreColumn]
        public string UserDisplayName { get; set; }

        [IgnoreColumn]
        public string UserEmail { get; set; }
    }
}