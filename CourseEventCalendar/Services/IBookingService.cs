using CourseEventCalendar.CourseEventCalendar.Models;
using System.Collections.Generic;

namespace CourseEventCalendar.CourseEventCalendar.Services
{
    public interface IBookingService
    {
        /// <summary>
        /// Creates a new booking for a user
        /// </summary>
        /// <param name="eventId">The event ID to book</param>
        /// <param name="userId">The user ID making the booking</param>
        /// <param name="notes">Optional notes for the booking</param>
        /// <returns>The created booking</returns>
        Booking CreateBooking(int eventId, int userId, string notes = null);

        /// <summary>
        /// Cancels an existing booking
        /// </summary>
        /// <param name="bookingId">The booking ID to cancel</param>
        /// <returns>True if successful, false if not</returns>
        bool CancelBooking(int bookingId);

        /// <summary>
        /// Gets all bookings for a specific user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of bookings</returns>
        IEnumerable<Booking> GetUserBookings(int userId);

        /// <summary>
        /// Gets all bookings for a specific event
        /// </summary>
        /// <param name="eventId">The event ID</param>
        /// <returns>List of bookings</returns>
        IEnumerable<Booking> GetEventBookings(int eventId);

        /// <summary>
        /// Gets a booking by ID
        /// </summary>
        /// <param name="bookingId">The booking ID</param>
        /// <returns>The booking or null if not found</returns>
        Booking GetBookingById(int bookingId);

        /// <summary>
        /// Checks if a user has already booked a specific event
        /// </summary>
        /// <param name="eventId">The event ID</param>
        /// <param name="userId">The user ID</param>
        /// <returns>True if the user has a booking, false otherwise</returns>
        bool UserHasBooking(int eventId, int userId);
    }
}