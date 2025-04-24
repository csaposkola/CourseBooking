using DotNetNuke.ComponentModel.DataAnnotations;
using System;
using System.Web.Caching;

namespace CourseBooking.Models
{
    [TableName("CoursePlans")]
    [PrimaryKey(nameof(ID), AutoIncrement = true)]
    [Cacheable("CoursePlan", CacheItemPriority.Default, 20)]
    [Scope("ModuleId")]
    public class CoursePlanEntity
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int DurationHours { get; set; }
        public string Description { get; set; }
        public int MaxCapacity { get; set; }
        public decimal Price { get; set; }
        public bool IsPublic { get; set; }
        public string PrerequisiteIds { get; set; }
        public string CourseCategory { get; set; }
    }
}