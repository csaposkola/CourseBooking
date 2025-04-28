using System;
using System.Globalization;

namespace CourseEventCalendar.CourseEventCalendar.Util 
{
    public struct WeekOfYear
    {
        public WeekOfYear(int year, int week)
        {
            Year = year;
            Week = week;
        }

        public WeekOfYear(DateTime date)
        {
            Year = date.Year;
            Week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date.Date,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday
                );
        }

        public int Year { get; }

        public int Week { get; }

        public DateTime FirstDay => DateUtil.FirstDateOfWeekISO8601(Year, Week);
        public DateTime LastDay => FirstDay.AddDays(7);

        public WeekOfYear AddWeeks(int week)
            => new WeekOfYear(FirstDay.AddDays(week * 7));
    }
}