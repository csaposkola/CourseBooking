using CourseBooking.Models;
using CourseBooking.Services;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Web.Mvc.Framework.ActionFilters;
using DotNetNuke.Web.Mvc.Framework.Controllers;
using System;
using System.Linq;
using System.Web.Mvc;

namespace CourseBooking.Controllers
{
    [DnnHandleError]
    public class CalendarController : DnnController
    {
        private IBookingService _bookingService;

        // Lazy-initialize the booking service
        protected IBookingService BookingService
        {
            get
            {
                if (_bookingService == null && ActiveModule != null)
                {
                    _bookingService = new BookingService(ActiveModule.ModuleID, PortalSettings.PortalId);
                }
                return _bookingService;
            }
        }

        [ModuleAction(ControlKey = "Edit", TitleKey = "Edit")]
        public ActionResult Index()
        {
            // Request required scripts for the calendar view
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQuery);
            DotNetNuke.Framework.JavaScriptLibraries.JavaScript.RequestRegistration(CommonJs.jQueryUI);
            DotNetNuke.Framework.ServicesFramework.Instance.RequestAjaxAntiForgerySupport();

            // Get current year and month
            var currentDate = DateTime.Now;
            ViewBag.CurrentYear = currentDate.Year;
            ViewBag.CurrentMonth = currentDate.Month;
            ViewBag.IsAdmin = User.IsInRole("Administrators") || User.IsSuperUser;

            return View();
        }

        public ActionResult Create(int year, int month, int day, int hour)
        {
            // Create proposed start time
            var startTime = new DateTime(year, month, day, hour, 0, 0);

            // Get all course plans for dropdown
            bool isAdmin = User.IsInRole("Administrators") || User.IsSuperUser;
            var coursePlans = BookingService.FindCoursePlans(isAdmin);

            ViewBag.StartTime = startTime;
            ViewBag.CoursePlans = coursePlans;

            return View();
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Create(BookingEntity booking, string[] participantNames, string[] participantEmails)
        {
            if (!ModelState.IsValid)
            {
                bool isAdmin = User.IsInRole("Administrators") || User.IsSuperUser;
                ViewBag.CoursePlans = BookingService.FindCoursePlans(isAdmin);
                ViewBag.StartTime = booking.StartTime;
                return View(booking);
            }

            try
            {
                // Set the creator ID and created date
                booking.CreatedByUserID = DotNetNuke.Entities.Users.UserController.Instance.GetCurrentUserInfo().UserID;
                booking.CreatedDate = DateTime.UtcNow;
                booking.IsCancelled = false;
                booking.PaymentStatus = "Pending";

                // Create the booking
                var newBooking = BookingService.CreateBooking(booking);

                // Add participants
                if (participantNames != null && participantEmails != null)
                {
                    for (int i = 0; i < participantNames.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(participantNames[i]) && !string.IsNullOrEmpty(participantEmails[i]))
                        {
                            var participant = new ParticipantEntity
                            {
                                ParticipantName = participantNames[i],
                                Email = participantEmails[i],
                                AddedByUserID = DotNetNuke.Entities.Users.UserController.Instance.GetCurrentUserInfo().UserID
                            };

                            BookingService.AddParticipantToBooking(newBooking.ID, participant);
                        }
                    }
                }

                // Send confirmation emails
                BookingService.SendBookingConfirmation(newBooking.ID);

                return RedirectToAction("Detail", new { id = newBooking.ID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating booking: " + ex.Message);
                bool isAdmin = User.IsInRole("Administrators") || User.IsSuperUser;
                ViewBag.CoursePlans = BookingService.FindCoursePlans(isAdmin);
                ViewBag.StartTime = booking.StartTime;
                return View(booking);
            }
        }

        public ActionResult Detail(int id)
        {
            var booking = BookingService.FindBookingById(id);
            if (booking == null)
            {
                return HttpNotFound();
            }

            // Check authorization
            bool isAdmin = User.IsInRole("Administrators") || User.IsSuperUser;
            bool isOwner = booking.CreatedByUserID == DotNetNuke.Entities.Users.UserController.Instance.GetCurrentUserInfo().UserID;

            if (!isAdmin && !isOwner)
            {
                return new HttpUnauthorizedResult();
            }

            // Get participants
            using (var ctx = DotNetNuke.Data.DataContext.Instance())
            {
                var participants = ctx.GetRepository<ParticipantEntity>()
                    .Find("WHERE BookingID = @0", id).ToList();
                ViewBag.Participants = participants;
            }

            ViewBag.IsAdmin = isAdmin;
            ViewBag.CanCancel = !booking.IsCancelled && booking.StartTime > DateTime.UtcNow;

            return View(booking);
        }

        [HttpPost]
        [DotNetNuke.Web.Mvc.Framework.ActionFilters.ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var booking = BookingService.FindBookingById(id);
            if (booking == null)
            {
                return HttpNotFound();
            }

            // Check authorization
            bool isAdmin = User.IsInRole("Administrators") || User.IsSuperUser;
            bool isOwner = booking.CreatedByUserID == DotNetNuke.Entities.Users.UserController.Instance.GetCurrentUserInfo().UserID;

            if (!isAdmin && !isOwner)
            {
                return new HttpUnauthorizedResult();
            }

            if (BookingService.CancelBooking(id))
            {
                return RedirectToAction("Detail", new { id = id });
            }
            else
            {
                ModelState.AddModelError("", "Failed to cancel booking.");
                return RedirectToAction("Detail", new { id = id });
            }
        }
    }
}