using CourseEventCalendar.CourseEventCalendar.Models;
using CourseEventCalendar.CourseEventCalendar.Services;
using CourseEventCalendar.CourseEventCalendar.Util;
using System;
using System.Net.Http;
using System.Web.Http;

namespace CourseEventCalendar.CourseEventCalendar.Controllers.Api
{
    public class CourseEventController : CourseApiControllerBase
    {
        public CourseEventController(
            ICourseEventManager courseManager
            ) : base(courseManager) { }
        
        public HttpResponseMessage Get(int id)
        {
            try
            {
                var courseEvent = CourseManager.FindEventByID(id);
                return Json(new { courseEvent });
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }
        
        public HttpResponseMessage List(int year, int week)
        {
            try
            {
                var from = DateUtil.FirstDateOfWeekISO8601(year, week);
                var to = from.AddDays(8).AddSeconds(-1);

                var events = CourseManager.FindEventsByDate(
                    from,
                    to,
                    false
                    );

                return Json(new { from, to, events });
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }

        [HttpPost]
        public HttpResponseMessage Create([FromBody] CreateCourseParameters args)
        {
            try
            {
                var courseEvent = new CourseEvent()
                {
                    StartAt = args.StartAt,
                    TemplateID = args.TemplateID
                };

                courseEvent = CourseManager.CreateEvent(
                    courseEvent
                    );

                return Json(courseEvent);
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }

        [HttpPost]
        public HttpResponseMessage Cancel([FromBody] CancelCourseParameters args)
        {
            try
            {
                CourseManager.CancelEvent(args.EventID);

                return JsonOk();
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }

        [HttpPost]
        public HttpResponseMessage Add([FromBody] AddParticipantParameters args)
        {
            try
            {
                if (!args.Validate())
                    return Json(401, "Invalid participant data.");

                var participant = new CourseParticipant()
                {
                    ParticipantName = args.Name,
                    Role = args.Role,
                    CertificateNumber = args.Certificate,
                };

                participant = CourseManager.AddParticipantTo(args.EventID, participant);

                return Json(participant);
            }
            catch (Exception ex)
            {
                return JsonException(ex);
            }
        }
    }
}