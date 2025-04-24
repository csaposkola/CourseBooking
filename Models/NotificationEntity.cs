using DotNetNuke.ComponentModel.DataAnnotations;
using System;
using System.Web.Caching;

namespace CourseBooking.Models
{
    [TableName("CourseNotifications")]
    [PrimaryKey(nameof(ID), AutoIncrement = true)]
    [Cacheable("CourseNotification", CacheItemPriority.Default, 20)]
    [Scope("ModuleId")]
    public class NotificationEntity
    {
        public int ID { get; set; }
        public int BookingID { get; set; }
        public string NotificationType { get; set; }
        public string Recipients { get; set; }
        public DateTime SentDate { get; set; }
        public string TemplateUsed { get; set; }
        public bool IsDeliverySuccessful { get; set; }
        public string ErrorLog { get; set; }

        [IgnoreColumn]
        public BookingEntity Booking { get; set; }
    }
}