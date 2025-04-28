using System;
using System.Runtime.Serialization;

namespace CourseEventCalendar.CourseEventCalendar.Services.Implementations
{
    [Serializable]
    internal class CourseEventException : Exception
    {
        public CourseEventException()
        {
        }

        public CourseEventException(string message) : base(message)
        {
        }

        public CourseEventException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CourseEventException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}