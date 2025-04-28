using CourseEventCalendar.CourseEventCalendar.Models;
using CourseEventCalendar.CourseEventCalendar.Services.Implementations;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CourseEventCalendar.CourseEventCalendar.Services.Implementations
{
    public class BookingService : IBookingService
    {
        private readonly ICourseEventManager _courseEventManager;

        public BookingService(ICourseEventManager courseEventManager)
        {
            _courseEventManager = courseEventManager
                ?? throw new ArgumentNullException(nameof(courseEventManager));
        }

        public bool CancelBooking(int bookingId)
        {
            using (var ctx = DataContext.Instance())
            {
                var bookingRepo = ctx.GetRepository<Booking>();
                var booking = bookingRepo.GetById(bookingId);

                if (booking == null)
                    return false;

                // Update event participant count
                var eventRepo = ctx.GetRepository<CourseEvent>();
                var courseEvent = eventRepo.GetById(booking.EventID);

                if (courseEvent != null)
                {
                    courseEvent.CurrentParticipants = Math.Max(0, courseEvent.CurrentParticipants - 1);
                    eventRepo.Update(courseEvent);
                }

                // Delete the booking
                bookingRepo.Delete(booking);

                return true;
            }
        }

        public Booking CreateBooking(int eventId, int userId, string notes = null)
        {
            using (var ctx = DataContext.Instance())
            {
                // First check if the event exists and has available seats
                var eventRepo = ctx.GetRepository<CourseEvent>();
                var courseEvent = eventRepo.GetById(eventId);

                if (courseEvent == null)
                    throw new ApplicationException("Event not found");

                if (courseEvent.IsCancelled)
                    throw new ApplicationException("Cannot book a cancelled event");

                if (courseEvent.CurrentParticipants >= courseEvent.MaxParticipants)
                    throw new ApplicationException("Event is fully booked");

                // Check if user already has a booking
                if (UserHasBooking(eventId, userId))
                    throw new ApplicationException("User already has a booking for this event");

                // Create booking
                var booking = new Booking
                {
                    EventID = eventId,
                    UserID = userId,
                    BookingDate = DateTime.UtcNow,
                    Status = "Confirmed",
                    Notes = notes
                };

                var bookingRepo = ctx.GetRepository<Booking>();
                bookingRepo.Insert(booking);

                // Update event participant count
                courseEvent.CurrentParticipants++;
                eventRepo.Update(courseEvent);

                return booking;
            }
        }

        public IEnumerable<Booking> GetEventBookings(int eventId)
        {
            using (var ctx = DataContext.Instance())
            {
                var bookingRepo = ctx.GetRepository<Booking>();
                return bookingRepo.Find("WHERE EventID = @0 AND Status = 'Confirmed'", eventId).ToList();
            }
        }

        public Booking GetBookingById(int bookingId)
        {
            using (var ctx = DataContext.Instance())
            {
                var bookingRepo = ctx.GetRepository<Booking>();
                var booking = bookingRepo.GetById(bookingId);

                if (booking == null)
                    return null;

                // Load related event and template data
                var eventRepo = ctx.GetRepository<CourseEvent>();
                var templateRepo = ctx.GetRepository<CourseTemplate>();

                booking.Event = eventRepo.GetById(booking.EventID);

                if (booking.Event != null)
                    booking.Template = templateRepo.GetById(booking.Event.TemplateID);

                return booking;
            }
        }

        public IEnumerable<Booking> GetUserBookings(int userId)
        {
            using (var ctx = DataContext.Instance())
            {
                var bookingRepo = ctx.GetRepository<Booking>();
                var bookings = bookingRepo.Find("WHERE UserID = @0 AND Status = 'Confirmed'", userId).ToList();

                // Load related event and template data for each booking
                var eventRepo = ctx.GetRepository<CourseEvent>();
                var templateRepo = ctx.GetRepository<CourseTemplate>();

                foreach (var booking in bookings)
                {
                    booking.Event = eventRepo.GetById(booking.EventID);

                    if (booking.Event != null)
                        booking.Template = templateRepo.GetById(booking.Event.TemplateID);
                }

                return bookings;
            }
        }

        public bool UserHasBooking(int eventId, int userId)
        {
            using (var ctx = DataContext.Instance())
            {
                var bookingRepo = ctx.GetRepository<Booking>();
                var booking = bookingRepo.Find("WHERE EventID = @0 AND UserID = @1 AND Status = 'Confirmed'", eventId, userId).FirstOrDefault();
                return booking != null;
            }
        }
    }
}