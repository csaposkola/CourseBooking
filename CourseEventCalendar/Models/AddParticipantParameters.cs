using System.Text.RegularExpressions;

namespace CourseEventCalendar.CourseEventCalendar.Models
{
    public class AddParticipantParameters
    {
        public int EventID { get; set; }

        public string Role { get; set; }

        public string Name { get; set; }

        public string Certificate { get; set; }

        public bool Validate()
        {
            var regex = new Regex(@"([a-zA-Z\d]{3})-(\d{5})-(\d{3})-(\d{4})-[sSmMlLcC]");
            return !string.IsNullOrWhiteSpace(Role)
                && !string.IsNullOrWhiteSpace(Name)
                && (!"instructor".Equals(Role) || regex.IsMatch(Certificate));
        }
    }
}