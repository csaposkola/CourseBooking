using DotNetNuke.ComponentModel.DataAnnotations;
using System.Web.Caching;

namespace CourseEventCalendar.CourseEventCalendar.Models
{
    [TableName("CourseEventCalendar_CourseTemplates")]
    [PrimaryKey(nameof(TemplateID), AutoIncrement = true)]
    [Cacheable("CourseTemplate", CacheItemPriority.Default, 20)]
    [Scope("ModuleId")]
    public class CourseTemplate
    {
        public int TemplateID { get; set; }

        public string Name { get; set; }

        public int Duration { get; set; }

        public string Description { get; set; }

        public bool IsPublic { get; set; }

        public int MaxParticipants { get; set; } = 10;

        [IgnoreColumn]
        public int TotalDuration => Duration + 1;

        [IgnoreColumn]
        public string CourseCategory { get; set; }
    }
}