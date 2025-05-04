// Models/CourseEvent.cs
using System;

namespace Csaposkola.Modules.Kurzusnaptar.Models
{
    public class CourseEvent
    {
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public decimal SitePrice { get; set; }
        public int InventoryCount { get; set; }
        public string ProductLink { get; set; }

        // Parsed values from SKU
        public DateTime StartDate { get; set; }
        public int DurationHours { get; set; }
    }
}