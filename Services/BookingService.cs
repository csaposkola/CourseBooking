using CourseBooking.Models;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Data;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using DotNetNuke.Services.Exceptions;

namespace CourseBooking.Services
{
    public class BookingException : Exception
    {
        public BookingException(string message) : base(message) { }
        public BookingException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class BookingService : ServiceLocator<IBookingService, BookingService>, IBookingService
    {
        private readonly IDataContext _dataContext;
        private const int DefaultCancellationHours = 24;

        private int ModuleId { get; set; } = -1;
        private int PortalId { get; set; } = -1;

        public BookingService()
        {
            _dataContext = DataContext.Instance();
        }

        public BookingService(int moduleId, int portalId) : this()
        {
            SetContextInternal(moduleId, portalId);
        }

        public void SetContext(int moduleId, int portalId)
        {
            SetContextInternal(moduleId, portalId);
        }

        private void SetContextInternal(int moduleId, int portalId)
        {
            Requires.NotNegative("moduleId", moduleId);
            Requires.NotNegative("portalId", portalId);
            this.ModuleId = moduleId;
            this.PortalId = portalId;
        }

        private void EnsureContext()
        {
            if (this.ModuleId < 0 || this.PortalId < 0)
            {
                var httpContext = System.Web.HttpContext.Current;
                if (httpContext != null) {
                    var moduleInfoQS = httpContext.Request.QueryString["moduleId"];
                    if (!string.IsNullOrEmpty(moduleInfoQS) && int.TryParse(moduleInfoQS, out int mid)) { this.ModuleId = mid; }
                    var portalSettings = httpContext.Items["PortalSettings"] as DotNetNuke.Entities.Portals.PortalSettings;
                    if (portalSettings != null) { this.PortalId = portalSettings.PortalId; }
                }
                if (this.ModuleId < 0 || this.PortalId < 0) {
                     throw new InvalidOperationException("BookingService context (ModuleId/PortalId) not initialized.");
                } else {
                    LogWarn("BookingService context initialized via HttpContext fallback.");
                }
            }
        }

        private T GetSetting<T>(string key, T defaultValue)
        {
            EnsureContext();
            var moduleInfo = ModuleController.Instance.GetModule(this.ModuleId, Null.NullInteger, true);
            if (moduleInfo == null) { 
                LogWarn($"ModuleInfo null for MId {this.ModuleId}. Using default for '{key}'."); 
                return defaultValue; 
            }
            
            if (typeof(T) == typeof(bool))
            {
                bool result;
                if (bool.TryParse(moduleInfo.ModuleSettings[key]?.ToString(), out result))
                {
                    return (T)(object)result;
                }
            }
            else if (typeof(T) == typeof(int))
            {
                int result;
                if (int.TryParse(moduleInfo.ModuleSettings[key]?.ToString(), out result))
                {
                    return (T)(object)result;
                }
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)(moduleInfo.ModuleSettings[key]?.ToString() ?? string.Empty);
            }
            
            return defaultValue;
        }

        private void LogError(string message, Exception ex = null) { Exceptions.LogException(ex ?? new Exception(message)); }
        private void LogWarn(string message) { Exceptions.LogException(new Exception("WARN: " + message)); }

        private void CheckPositiveId(string paramName, int id)
        {
            Requires.NotNegative(paramName, id);
            if (id <= 0) throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be positive.");
        }

        #region Course Plan Operations

        public IEnumerable<CoursePlanEntity> GetCoursePlans(bool includeNonPublic = false)
        {
            EnsureContext();
            try {
                var repo = _dataContext.GetRepository<CoursePlanEntity>();
                return includeNonPublic ? repo.Get().ToList() : repo.Find("WHERE IsPublic = 1").ToList();
            } catch (Exception ex) { LogError("Error getting course plans.", ex); return Enumerable.Empty<CoursePlanEntity>(); }
        }

        public CoursePlanEntity GetCoursePlanById(int coursePlanId)
        {
            EnsureContext();
            CheckPositiveId(nameof(coursePlanId), coursePlanId);
            try {
                 return _dataContext.GetRepository<CoursePlanEntity>().Find("WHERE ID = @0", coursePlanId).FirstOrDefault();
            } catch (Exception ex) { LogError($"Error getting course plan by ID: {coursePlanId}", ex); return null; }
        }

        private IDictionary<int, CoursePlanEntity> GetCoursePlansByIds(IDataContext ctx, List<int> planIds)
        {
            if (planIds == null || !planIds.Any()) return new Dictionary<int, CoursePlanEntity>();
            try {
                return ctx.GetRepository<CoursePlanEntity>().Find("WHERE ID IN (@0)", planIds).ToDictionary(p => p.ID);
            } catch (Exception ex) { LogError("Error fetching multiple course plans.", ex); return new Dictionary<int, CoursePlanEntity>(); }
        }

        #endregion

        #region Course Schedule Operations

        public IEnumerable<CourseScheduleEntity> GetCourseSchedules(DateTime? fromDate = null, DateTime? toDate = null, bool includeInactive = false)
        {
            EnsureContext();
            try {
                var query = "WHERE ModuleId = @0"; List<object> args = new List<object> { this.ModuleId }; int argIndex = 1;
                if (!includeInactive) { query += " AND IsActive = 1"; }
                if (fromDate.HasValue) { query += $" AND StartTime >= @{argIndex++}"; args.Add(fromDate.Value.ToUniversalTime()); }
                if (toDate.HasValue) { DateTime endOfDayUtc = toDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime(); query += $" AND StartTime <= @{argIndex++}"; args.Add(endOfDayUtc); }
                query += " ORDER BY StartTime ASC";
                var schedules = _dataContext.GetRepository<CourseScheduleEntity>().Find(query, args.ToArray()).ToList();
                if (schedules.Any()) {
                    var planIds = schedules.Select(s => s.CoursePlanID).Distinct().ToList();
                    var scheduleIds = schedules.Select(s => s.ID).Distinct().ToList();
                    var plans = GetCoursePlansByIds(_dataContext, planIds);
                    var bookingCounts = GetBookingCountsForSchedules(_dataContext, scheduleIds);
                    foreach (var schedule in schedules) {
                        if (plans.TryGetValue(schedule.CoursePlanID, out var plan)) { schedule.CoursePlan = plan; }
                        schedule.BookingCount = bookingCounts.TryGetValue(schedule.ID, out var count) ? count : 0;
                    }
                }
                return schedules;
            } catch (Exception ex) { LogError("Error getting course schedules.", ex); return Enumerable.Empty<CourseScheduleEntity>(); }
        }

        public CourseScheduleEntity GetCourseScheduleById(int scheduleId)
        {
            EnsureContext();
            CheckPositiveId(nameof(scheduleId), scheduleId);
            try {
                var schedule = _dataContext.GetRepository<CourseScheduleEntity>().Find("WHERE ID = @0 AND ModuleId = @1", scheduleId, this.ModuleId).FirstOrDefault();
                if (schedule != null) {
                    schedule.CoursePlan = GetCoursePlanById(schedule.CoursePlanID);
                    schedule.BookingCount = GetBookingCountForSchedule(schedule.ID);
                }
                return schedule;
            } catch (Exception ex) { LogError($"Error getting schedule by ID: {scheduleId}", ex); return null; }
        }

        public CourseScheduleEntity CreateCourseSchedule(CourseScheduleEntity schedule)
        {
            EnsureContext(); Requires.NotNull("schedule", schedule);
            CheckPositiveId(nameof(schedule.CoursePlanID), schedule.CoursePlanID);
            try {
                var plan = GetCoursePlanById(schedule.CoursePlanID);
                if (plan == null) throw new ArgumentException($"Invalid CoursePlanID: {schedule.CoursePlanID}");
                schedule.ModuleId = ModuleId; schedule.CreatedDate = DateTime.UtcNow;
                if (schedule.AvailableSeats <= 0) schedule.AvailableSeats = plan.MaxCapacity;
                if (schedule.AvailableSeats < 0) schedule.AvailableSeats = 0;
                if (schedule.StartTime.Kind != DateTimeKind.Utc) { LogWarn($"Non-UTC StartTime ({schedule.StartTime.Kind}), converting."); schedule.StartTime = schedule.StartTime.ToUniversalTime(); }
                _dataContext.GetRepository<CourseScheduleEntity>().Insert(schedule);
                LogWarn($"Created Schedule ID: {schedule.ID}"); return schedule;
            } catch (Exception ex) { LogError("Error creating schedule.", ex); throw; }
        }

        public bool UpdateCourseSchedule(CourseScheduleEntity schedule)
        {
            EnsureContext(); Requires.NotNull("schedule", schedule);
            CheckPositiveId(nameof(schedule.ID), schedule.ID);
            try {
                var existing = _dataContext.GetRepository<CourseScheduleEntity>().Find("WHERE ID = @0 AND ModuleId = @1", schedule.ID, this.ModuleId).FirstOrDefault();
                if (existing == null) throw new BookingException("Schedule not found or access denied.");
                int currentBookings = GetBookingCountForSchedule(schedule.ID);
                if (schedule.AvailableSeats < currentBookings) { throw new BookingException($"Seats ({schedule.AvailableSeats}) < current bookings ({currentBookings})."); }
                schedule.ModuleId = this.ModuleId;
                if (schedule.StartTime.Kind != DateTimeKind.Utc) { LogWarn($"Non-UTC StartTime ({schedule.StartTime.Kind}), converting."); schedule.StartTime = schedule.StartTime.ToUniversalTime(); }
                _dataContext.GetRepository<CourseScheduleEntity>().Update(schedule); return true;
            } catch (BookingException) { throw; }
              catch (Exception ex) { LogError($"Error updating schedule ID: {schedule.ID}", ex); return false; }
        }

        public bool DeleteCourseSchedule(int scheduleId)
        {
            EnsureContext(); CheckPositiveId(nameof(scheduleId), scheduleId);
            try {
                var schedule = GetCourseScheduleById(scheduleId);
                if (schedule == null) return false;
                schedule.IsActive = false;
                _dataContext.GetRepository<CourseScheduleEntity>().Update(schedule);
                LogWarn($"Marked Schedule ID {scheduleId} as inactive."); return true;
            } catch (BookingException bex) { LogError($"Biz rule violation deleting schedule {scheduleId}. {bex.Message}"); return false; }
              catch (Exception ex) { LogError($"Error deleting/deactivating schedule {scheduleId}", ex); return false; }
        }

        #endregion

        #region Booking Operations

        public IEnumerable<BookingEntity> GetBookingsByUser(int userId)
        {
            EnsureContext(); CheckPositiveId(nameof(userId), userId);
            try {
                var bookings = _dataContext.GetRepository<BookingEntity>().Find("WHERE UserID = @0 AND ModuleId = @1 ORDER BY BookingTime DESC", userId, this.ModuleId).ToList();
                LoadBookingDetails(_dataContext, bookings); return bookings;
            } catch(Exception ex) { LogError($"Error getting bookings for user {userId}", ex); return Enumerable.Empty<BookingEntity>(); }
        }

        public IEnumerable<BookingEntity> GetBookingsByCourseSchedule(int scheduleId)
        {
            EnsureContext(); CheckPositiveId(nameof(scheduleId), scheduleId);
             try {
                var bookings = _dataContext.GetRepository<BookingEntity>().Find("WHERE CourseScheduleID = @0 AND ModuleId = @1 ORDER BY BookingTime", scheduleId, this.ModuleId).ToList();
                LoadBookingDetails(_dataContext, bookings); return bookings;
             } catch (Exception ex) { LogError($"Error getting bookings for schedule {scheduleId}", ex); return Enumerable.Empty<BookingEntity>(); }
        }

        public BookingEntity GetBookingById(int bookingId)
        {
            EnsureContext(); CheckPositiveId(nameof(bookingId), bookingId);
            try {
                var booking = _dataContext.GetRepository<BookingEntity>().Find("WHERE ID = @0 AND ModuleId = @1", bookingId, this.ModuleId).FirstOrDefault();
                if (booking != null) { LoadBookingDetails(_dataContext, new List<BookingEntity> { booking }); } return booking;
            } catch (Exception ex) { LogError($"Error getting booking by ID: {bookingId}", ex); return null; }
        }

        public BookingEntity CreateBooking(int courseScheduleId, int userId, string notes = null)
        {
            EnsureContext(); CheckPositiveId(nameof(courseScheduleId), courseScheduleId); CheckPositiveId(nameof(userId), userId);
            try {
                var schedule = GetCourseScheduleById(courseScheduleId);
                if (schedule == null || !schedule.IsActive) throw new BookingException("Schedule not found or inactive.");
                if (schedule.StartTime <= DateTime.UtcNow) throw new BookingException("Course has started.");
                if (IsUserRegisteredForSchedule(courseScheduleId, userId)) throw new BookingException("Already registered.");
                if (schedule.RemainingSeats <= 0) throw new BookingException("No available seats.");
                var booking = new BookingEntity {
                    ModuleId = this.ModuleId,
                    CourseScheduleID = courseScheduleId,
                    UserID = userId,
                    BookingTime = DateTime.UtcNow,
                    IsCancelled = false,
                    VoucherCode = GenerateVoucherCode(),
                    PaymentStatus = "Pending",
                    Notes = notes?.Length > 500 ? notes.Substring(0, 500) : notes
                };
                _dataContext.GetRepository<BookingEntity>().Insert(booking);
                LogWarn($"Created Booking {booking.ID} for User {userId}, Schedule {courseScheduleId}");
                LoadBookingDetails(_dataContext, new List<BookingEntity> { booking }); return booking;
            } catch (BookingException) { throw; }
              catch (Exception ex) { LogError($"Error creating booking User {userId}, Schedule {courseScheduleId}.", ex); throw new BookingException("Unexpected error creating booking.", ex); }
        }

        public bool CancelBooking(int bookingId)
        {
            EnsureContext(); CheckPositiveId(nameof(bookingId), bookingId);
            try {
                var booking = GetBookingById(bookingId);
                if (booking == null) return false; if (booking.IsCancelled) return true;
                if (booking.CourseSchedule == null) throw new BookingException("Schedule data missing.");
                int cancellationHours = GetSetting<int>("CourseBooking_CancellationHours", DefaultCancellationHours);
                if (booking.CourseSchedule.StartTime < DateTime.UtcNow.AddHours(cancellationHours)) { throw new BookingException($"Cannot cancel < {cancellationHours} hours before start."); }
                booking.IsCancelled = true; _dataContext.GetRepository<BookingEntity>().Update(booking);
                LogWarn($"Cancelled Booking {bookingId}"); return true;
            } catch (BookingException bex) { LogError($"Biz rule violation cancelling booking {bookingId}. {bex.Message}"); throw; }
              catch (Exception ex) { LogError($"Error cancelling booking {bookingId}", ex); return false; }
        }

        #endregion

        #region Helper Methods

        private IDictionary<int, int> GetBookingCountsForSchedules(IDataContext ctx, List<int> scheduleIds)
        {
             if (scheduleIds == null || !scheduleIds.Any()) return new Dictionary<int, int>();
             try {
                 var results = ctx.ExecuteQuery<dynamic>(
                     CommandType.Text,
                     "SELECT CourseScheduleID, COUNT(*) as Count FROM {databaseOwner}[{objectQualifier}CourseBookings] WHERE ModuleId = @1 AND CourseScheduleID IN (@0) AND IsCancelled = 0 GROUP BY CourseScheduleID",
                     new object[] { scheduleIds, this.ModuleId }
                 );
                
                return results.ToDictionary(row => (int)row.CourseScheduleID, row => (int)row.Count);
             } catch (Exception ex) { LogError("Error fetching multiple booking counts.", ex); return new Dictionary<int, int>(); }
        }

        public int GetBookingCountForSchedule(int scheduleId)
        {
            EnsureContext(); CheckPositiveId(nameof(scheduleId), scheduleId);
            try {
                return _dataContext.ExecuteScalar<int>(
                    CommandType.Text,
                    "SELECT COUNT(*) FROM {databaseOwner}[{objectQualifier}CourseBookings] WHERE CourseScheduleID = @0 AND ModuleId = @1 AND IsCancelled = 0",
                    scheduleId, this.ModuleId
                );
            } catch (Exception ex) { LogError($"Error getting count for schedule {scheduleId}", ex); return 0; }
        }

        public bool IsUserRegisteredForSchedule(int scheduleId, int userId)
        {
            EnsureContext(); CheckPositiveId(nameof(scheduleId), scheduleId); CheckPositiveId(nameof(userId), userId);
            try {
                return _dataContext.ExecuteScalar<int>(
                    CommandType.Text,
                    "SELECT COUNT(*) FROM {databaseOwner}[{objectQualifier}CourseBookings] WHERE CourseScheduleID = @0 AND UserID = @1 AND ModuleId = @2 AND IsCancelled = 0",
                    scheduleId, userId, this.ModuleId
                ) > 0;
            } catch (Exception ex) { LogError($"Error checking reg for User {userId}, Schedule {scheduleId}", ex); return false; }
        }

        private void LoadBookingDetails(IDataContext ctx, List<BookingEntity> bookings)
        {
            if (bookings == null || !bookings.Any()) return; EnsureContext();
            try {
                var scheduleIds = bookings.Select(b => b.CourseScheduleID).Distinct().ToList();
                var userIds = bookings.Select(b => b.UserID).Distinct().ToList();
                var schedules = scheduleIds.Any() ? ctx.GetRepository<CourseScheduleEntity>().Find("WHERE ID IN (@0)", scheduleIds).ToDictionary(s => s.ID) : new Dictionary<int, CourseScheduleEntity>();
                var planIds = schedules.Values.Select(s => s.CoursePlanID).Distinct().ToList();
                var plans = GetCoursePlansByIds(ctx, planIds);
                var users = userIds.Any() ? userIds.Select(uid => UserController.Instance.GetUserById(this.PortalId, uid)).Where(u => u != null).ToDictionary(u => u.UserID) : new Dictionary<int, UserInfo>();
                foreach (var booking in bookings) {
                    if (schedules.TryGetValue(booking.CourseScheduleID, out var schedule)) {
                        booking.CourseSchedule = schedule;
                        if (schedule != null && plans.TryGetValue(schedule.CoursePlanID, out var plan)) { schedule.CoursePlan = plan; }
                    } else { LogWarn($"Schedule {booking.CourseScheduleID} not found for Booking {booking.ID}"); }
                    if (users.TryGetValue(booking.UserID, out var user)) { booking.UserDisplayName = user.DisplayName; booking.UserEmail = user.Email; }
                    else { LogWarn($"User {booking.UserID} not found for Booking {booking.ID}"); booking.UserDisplayName = "Unknown User"; }
                 }
            } catch (Exception ex) { LogError("Error loading booking details.", ex); }
        }

        private string GenerateVoucherCode()
        {
            const string chars = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
            using (var rng = RandomNumberGenerator.Create()) {
                byte[] data = new byte[8]; rng.GetBytes(data); var result = new char[8];
                for (int i = 0; i < result.Length; i++) { result[i] = chars[data[i] % chars.Length]; }
                return "CRS-" + new string(result);
            }
        }

        #endregion

        protected override Func<IBookingService> GetFactory() { return () => new BookingService(); }
    }
}