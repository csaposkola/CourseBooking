using DotNetNuke.ComponentModel.DataAnnotations;
using System;

namespace CourseEventCalendar.CourseEventCalendar.Models
{
    [TableName("CourseEventCalendar_Bookings")]
    [PrimaryKey(nameof(BookingID), AutoIncrement = true)]
    public class Booking
    {
        public int BookingID { get; set; }

        public int EventID { get; set; }

        public int UserID { get; set; }

        public DateTime BookingDate { get; set; }

        public string Status { get; set; }

        public string Notes { get; set; }

        [IgnoreColumn]
        public CourseEvent Event { get; set; }

        [IgnoreColumn]
        public CourseTemplate Template { get; set; }
    }
}