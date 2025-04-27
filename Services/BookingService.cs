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

        // Keep internal state for methods where context might be implicitly expected (like Create/Update)
        // but prefer explicit parameters for lookups.
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

        // Method to explicitly set context if needed
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

        // This method is less critical if we pass moduleId explicitly for lookups
        private void EnsureContext()
        {
            if (this.ModuleId < 0 || this.PortalId < 0)
            {
                // Attempt to get context if not set, but don't throw if lookup methods get explicit IDs
                var httpContext = System.Web.HttpContext.Current;
                 if (httpContext != null) {
                    var moduleInfoQS = httpContext.Request.QueryString["moduleId"];
                    if (!string.IsNullOrEmpty(moduleInfoQS) && int.TryParse(moduleInfoQS, out int mid)) { this.ModuleId = mid; }
                    var portalSettings = httpContext.Items["PortalSettings"] as DotNetNuke.Entities.Portals.PortalSettings;
                    if (portalSettings != null) { this.PortalId = portalSettings.PortalId; }
                 }
                 if (this.ModuleId < 0 || this.PortalId < 0) {
                      LogWarn("BookingService context (ModuleId/PortalId) could not be automatically initialized.");
                      // Don't throw, allow explicit parameters to work
                 } else {
                     LogWarn("BookingService context initialized via HttpContext fallback.");
                 }
            }
        }


        private T GetSetting<T>(string key, T defaultValue, int moduleId) // Pass moduleId for settings
        {
            Requires.NotNegative("moduleId", moduleId);
            var moduleInfo = ModuleController.Instance.GetModule(moduleId, Null.NullInteger, true);
            if (moduleInfo == null) {
                LogWarn($"ModuleInfo null for MId {moduleId}. Using default for '{key}'.");
                return defaultValue;
            }

            if (moduleInfo.ModuleSettings.ContainsKey(key))
            {
                 var settingValue = moduleInfo.ModuleSettings[key]?.ToString();
                 try
                 {
                     return (T)Convert.ChangeType(settingValue, typeof(T));
                 }
                 catch (Exception ex)
                 {
                     LogWarn($"Failed to convert setting '{key}' (value: '{settingValue}') to type {typeof(T)}. Using default. Error: {ex.Message}");
                 }
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

        // Course Plans are global (not module specific), so no moduleId needed here
        public IEnumerable<CoursePlanEntity> GetCoursePlans(bool includeNonPublic = false)
        {
            try {
                var repo = _dataContext.GetRepository<CoursePlanEntity>();
                return includeNonPublic ? repo.Get().ToList() : repo.Find("WHERE IsPublic = 1").ToList();
            } catch (Exception ex) { LogError("Error getting course plans.", ex); return Enumerable.Empty<CoursePlanEntity>(); }
        }

        public CoursePlanEntity GetCoursePlanById(int coursePlanId)
        {
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

        public IEnumerable<CourseScheduleEntity> GetCourseSchedules(int moduleId, DateTime? fromDate = null, DateTime? toDate = null, bool includeInactive = false)
        {
            Requires.NotNegative("moduleId", moduleId);
            try {
                var query = "WHERE ModuleId = @0"; List<object> args = new List<object> { moduleId }; int argIndex = 1;
                if (!includeInactive) { query += " AND IsActive = 1"; }
                if (fromDate.HasValue) { query += $" AND StartTime >= @{argIndex++}"; args.Add(fromDate.Value.ToUniversalTime()); }
                if (toDate.HasValue) { DateTime endOfDayUtc = toDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime(); query += $" AND StartTime <= @{argIndex++}"; args.Add(endOfDayUtc); }
                query += " ORDER BY StartTime ASC";
                var schedules = _dataContext.GetRepository<CourseScheduleEntity>().Find(query, args.ToArray()).ToList();
                if (schedules.Any()) {
                    var planIds = schedules.Select(s => s.CoursePlanID).Distinct().ToList();
                    var scheduleIds = schedules.Select(s => s.ID).Distinct().ToList();
                    var plans = GetCoursePlansByIds(_dataContext, planIds);
                    // Pass moduleId to GetBookingCountsForSchedules
                    var bookingCounts = GetBookingCountsForSchedules(_dataContext, scheduleIds, moduleId);
                    foreach (var schedule in schedules) {
                        if (plans.TryGetValue(schedule.CoursePlanID, out var plan)) { schedule.CoursePlan = plan; }
                        schedule.BookingCount = bookingCounts.TryGetValue(schedule.ID, out var count) ? count : 0;
                    }
                }
                return schedules;
            } catch (Exception ex) { LogError("Error getting course schedules.", ex); return Enumerable.Empty<CourseScheduleEntity>(); }
        }

        public CourseScheduleEntity GetCourseScheduleById(int scheduleId, int moduleId)
        {
            CheckPositiveId(nameof(scheduleId), scheduleId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                var schedule = _dataContext.GetRepository<CourseScheduleEntity>()
                    .Find("WHERE ID = @0 AND ModuleId = @1", scheduleId, moduleId) // Use passed moduleId
                    .FirstOrDefault();
                if (schedule != null) {
                    schedule.CoursePlan = GetCoursePlanById(schedule.CoursePlanID);
                    // Pass moduleId to GetBookingCountForSchedule
                    schedule.BookingCount = GetBookingCountForSchedule(schedule.ID, moduleId);
                }
                return schedule;
            } catch (Exception ex) { LogError($"Error getting schedule by ID: {scheduleId} for Module {moduleId}", ex); return null; }
        }

        public CourseScheduleEntity CreateCourseSchedule(CourseScheduleEntity schedule)
        {
            EnsureContext(); // Need context for CreatedByUserID and ModuleId storage
            Requires.NotNull("schedule", schedule);
            CheckPositiveId(nameof(schedule.CoursePlanID), schedule.CoursePlanID);
            try {
                var plan = GetCoursePlanById(schedule.CoursePlanID);
                if (plan == null) throw new ArgumentException($"Invalid CoursePlanID: {schedule.CoursePlanID}");
                schedule.ModuleId = this.ModuleId; // Use context ModuleId for storing
                schedule.CreatedDate = DateTime.UtcNow;
                // CreatedByUserID should be set in the controller before calling this
                if (schedule.AvailableSeats <= 0) schedule.AvailableSeats = plan.MaxCapacity;
                if (schedule.AvailableSeats < 0) schedule.AvailableSeats = 0;
                if (schedule.StartTime.Kind != DateTimeKind.Utc) { LogWarn($"Non-UTC StartTime ({schedule.StartTime.Kind}), converting."); schedule.StartTime = schedule.StartTime.ToUniversalTime(); }
                _dataContext.GetRepository<CourseScheduleEntity>().Insert(schedule);
                LogWarn($"Created Schedule ID: {schedule.ID} for Module {this.ModuleId}"); return schedule;
            } catch (Exception ex) { LogError("Error creating schedule.", ex); throw; }
        }

        public bool UpdateCourseSchedule(CourseScheduleEntity schedule)
        {
            EnsureContext(); // Need context for ModuleId check
            Requires.NotNull("schedule", schedule);
            CheckPositiveId(nameof(schedule.ID), schedule.ID);
            try {
                // Verify ownership using context ModuleId
                var existing = _dataContext.GetRepository<CourseScheduleEntity>().Find("WHERE ID = @0 AND ModuleId = @1", schedule.ID, this.ModuleId).FirstOrDefault();
                if (existing == null) throw new BookingException("Schedule not found or access denied.");

                // Use context ModuleId for getting count
                int currentBookings = GetBookingCountForSchedule(schedule.ID, this.ModuleId);
                if (schedule.AvailableSeats < currentBookings) { throw new BookingException($"Seats ({schedule.AvailableSeats}) < current bookings ({currentBookings})."); }

                schedule.ModuleId = this.ModuleId; // Ensure ModuleId remains correct
                if (schedule.StartTime.Kind != DateTimeKind.Utc) { LogWarn($"Non-UTC StartTime ({schedule.StartTime.Kind}), converting."); schedule.StartTime = schedule.StartTime.ToUniversalTime(); }
                _dataContext.GetRepository<CourseScheduleEntity>().Update(schedule); return true;
            } catch (BookingException) { throw; }
              catch (Exception ex) { LogError($"Error updating schedule ID: {schedule.ID}", ex); return false; }
        }

        public bool DeleteCourseSchedule(int scheduleId, int moduleId)
        {
            CheckPositiveId(nameof(scheduleId), scheduleId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                // Use passed moduleId for lookup and update
                var schedule = GetCourseScheduleById(scheduleId, moduleId); // Use passed ID
                if (schedule == null) return false; // Not found or wrong module

                // Check if there are active bookings before deactivating (optional business rule)
                // int currentBookings = GetBookingCountForSchedule(scheduleId, moduleId);
                // if (currentBookings > 0) { throw new BookingException("Cannot deactivate schedule with active bookings."); }

                schedule.IsActive = false; // Mark as inactive instead of deleting
                _dataContext.GetRepository<CourseScheduleEntity>().Update(schedule);
                LogWarn($"Marked Schedule ID {scheduleId} (Module {moduleId}) as inactive."); return true;
            } catch (BookingException bex) { LogError($"Biz rule violation deleting/deactivating schedule {scheduleId} (Module {moduleId}). {bex.Message}"); throw; }
              catch (Exception ex) { LogError($"Error deleting/deactivating schedule {scheduleId} (Module {moduleId})", ex); return false; }
        }

        #endregion

        #region Booking Operations

        public IEnumerable<BookingEntity> GetBookingsByUser(int userId, int moduleId)
        {
            CheckPositiveId(nameof(userId), userId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                var bookings = _dataContext.GetRepository<BookingEntity>()
                    .Find("WHERE UserID = @0 AND ModuleId = @1 ORDER BY BookingTime DESC", userId, moduleId) // Use passed moduleId
                    .ToList();
                // Pass moduleId to LoadBookingDetails
                LoadBookingDetails(_dataContext, bookings, moduleId);
                return bookings;
            } catch(Exception ex) { LogError($"Error getting bookings for user {userId}, module {moduleId}", ex); return Enumerable.Empty<BookingEntity>(); }
        }

        public IEnumerable<BookingEntity> GetBookingsByCourseSchedule(int scheduleId, int moduleId)
        {
            CheckPositiveId(nameof(scheduleId), scheduleId);
            Requires.NotNegative("moduleId", moduleId);
             try {
                var bookings = _dataContext.GetRepository<BookingEntity>()
                    .Find("WHERE CourseScheduleID = @0 AND ModuleId = @1 ORDER BY BookingTime", scheduleId, moduleId) // Use passed moduleId
                    .ToList();
                // Pass moduleId to LoadBookingDetails
                LoadBookingDetails(_dataContext, bookings, moduleId);
                return bookings;
             } catch (Exception ex) { LogError($"Error getting bookings for schedule {scheduleId}, module {moduleId}", ex); return Enumerable.Empty<BookingEntity>(); }
        }

        public BookingEntity GetBookingById(int bookingId, int moduleId)
        {
            CheckPositiveId(nameof(bookingId), bookingId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                var booking = _dataContext.GetRepository<BookingEntity>()
                    .Find("WHERE ID = @0 AND ModuleId = @1", bookingId, moduleId) // Use passed moduleId
                    .FirstOrDefault();
                if (booking != null) {
                    // Pass moduleId to LoadBookingDetails
                    LoadBookingDetails(_dataContext, new List<BookingEntity> { booking }, moduleId);
                }
                return booking;
            } catch (Exception ex) { LogError($"Error getting booking by ID: {bookingId}, module {moduleId}", ex); return null; }
        }

        public BookingEntity CreateBooking(int courseScheduleId, int userId, int moduleId, string notes = null)
        {
            CheckPositiveId(nameof(courseScheduleId), courseScheduleId);
            CheckPositiveId(nameof(userId), userId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                // Lookup schedule using passed moduleId
                var schedule = GetCourseScheduleById(courseScheduleId, moduleId);
                if (schedule == null || !schedule.IsActive) throw new BookingException("Schedule not found or inactive for this module.");
                if (schedule.StartTime <= DateTime.UtcNow) throw new BookingException("Course has started.");
                // Check registration using passed moduleId
                if (IsUserRegisteredForSchedule(courseScheduleId, userId, moduleId)) throw new BookingException("Already registered.");
                if (schedule.RemainingSeats <= 0) throw new BookingException("No available seats.");

                var booking = new BookingEntity {
                    ModuleId = moduleId, // Store the passed moduleId
                    CourseScheduleID = courseScheduleId,
                    UserID = userId,
                    BookingTime = DateTime.UtcNow,
                    IsCancelled = false,
                    VoucherCode = GenerateVoucherCode(),
                    PaymentStatus = "Pending",
                    Notes = notes?.Length > 500 ? notes.Substring(0, 500) : notes
                };
                _dataContext.GetRepository<BookingEntity>().Insert(booking);
                LogWarn($"Created Booking {booking.ID} for User {userId}, Schedule {courseScheduleId}, Module {moduleId}");
                // Pass moduleId to LoadBookingDetails
                LoadBookingDetails(_dataContext, new List<BookingEntity> { booking }, moduleId);
                return booking;
            } catch (BookingException) { throw; }
              catch (Exception ex) { LogError($"Error creating booking User {userId}, Schedule {courseScheduleId}, Module {moduleId}.", ex); throw new BookingException("Unexpected error creating booking.", ex); }
        }

        public bool CancelBooking(int bookingId, int moduleId)
        {
            CheckPositiveId(nameof(bookingId), bookingId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                // Lookup booking using passed moduleId
                var booking = GetBookingById(bookingId, moduleId);
                if (booking == null) return false; // Not found or belongs to another module
                if (booking.IsCancelled) return true; // Already cancelled

                // Ensure schedule data is loaded (GetBookingById should handle this)
                if (booking.CourseSchedule == null) throw new BookingException("Schedule data missing for booking.");

                // Get setting using passed moduleId
                int cancellationHours = GetSetting<int>("CourseBooking_CancellationHours", DefaultCancellationHours, moduleId);
                if (booking.CourseSchedule.StartTime < DateTime.UtcNow.AddHours(cancellationHours)) { throw new BookingException($"Cannot cancel < {cancellationHours} hours before start."); }

                booking.IsCancelled = true;
                _dataContext.GetRepository<BookingEntity>().Update(booking);
                LogWarn($"Cancelled Booking {bookingId} (Module {moduleId})");
                return true;
            } catch (BookingException bex) { LogError($"Biz rule violation cancelling booking {bookingId} (Module {moduleId}). {bex.Message}", bex); throw; }
              catch (Exception ex) { LogError($"Error cancelling booking {bookingId} (Module {moduleId})", ex); return false; }
        }

        #endregion

        #region Helper Methods

        // Pass ModuleId explicitly
        private IDictionary<int, int> GetBookingCountsForSchedules(IDataContext ctx, List<int> scheduleIds, int moduleId)
        {
             if (scheduleIds == null || !scheduleIds.Any()) return new Dictionary<int, int>();
             Requires.NotNegative("moduleId", moduleId);
             try {
                 var results = ctx.ExecuteQuery<dynamic>(
                     CommandType.Text,
                     "SELECT CourseScheduleID, COUNT(*) as Count FROM {databaseOwner}[{objectQualifier}CourseBookings] WHERE ModuleId = @1 AND CourseScheduleID IN (@0) AND IsCancelled = 0 GROUP BY CourseScheduleID",
                     new object[] { scheduleIds, moduleId } // Use passed moduleId
                 );

                return results.ToDictionary(row => (int)row.CourseScheduleID, row => (int)row.Count);
             } catch (Exception ex) { LogError($"Error fetching multiple booking counts for module {moduleId}.", ex); return new Dictionary<int, int>(); }
        }

        // Pass ModuleId explicitly
        public int GetBookingCountForSchedule(int scheduleId, int moduleId)
        {
            CheckPositiveId(nameof(scheduleId), scheduleId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                return _dataContext.ExecuteScalar<int>(
                    CommandType.Text,
                    "SELECT COUNT(*) FROM {databaseOwner}[{objectQualifier}CourseBookings] WHERE CourseScheduleID = @0 AND ModuleId = @1 AND IsCancelled = 0",
                    scheduleId, moduleId // Use passed moduleId
                );
            } catch (Exception ex) { LogError($"Error getting count for schedule {scheduleId}, module {moduleId}", ex); return 0; }
        }

        // Pass ModuleId explicitly
        public bool IsUserRegisteredForSchedule(int scheduleId, int userId, int moduleId)
        {
            CheckPositiveId(nameof(scheduleId), scheduleId);
            CheckPositiveId(nameof(userId), userId);
            Requires.NotNegative("moduleId", moduleId);
            try {
                return _dataContext.ExecuteScalar<int>(
                    CommandType.Text,
                    "SELECT COUNT(*) FROM {databaseOwner}[{objectQualifier}CourseBookings] WHERE CourseScheduleID = @0 AND UserID = @1 AND ModuleId = @2 AND IsCancelled = 0",
                    scheduleId, userId, moduleId // Use passed moduleId
                ) > 0;
            } catch (Exception ex) { LogError($"Error checking reg for User {userId}, Schedule {scheduleId}, Module {moduleId}", ex); return false; }
        }

        // Pass ModuleId explicitly
        private void LoadBookingDetails(IDataContext ctx, List<BookingEntity> bookings, int moduleId)
        {
            if (bookings == null || !bookings.Any()) return;
            Requires.NotNegative("moduleId", moduleId);
            // PortalId might still be needed for UserController.Instance.GetUserById
            EnsureContext(); // Ensure PortalId is available if possible
            int currentPortalId = this.PortalId;
             if (currentPortalId < 0)
             {
                  LogWarn("PortalId not available in LoadBookingDetails, cannot load user info.");
                  // Set display names to default
                  foreach (var booking in bookings) { booking.UserDisplayName = "Unknown User"; booking.UserEmail = "N/A"; }
                  // Still try to load schedule/plan data
             }

            try {
                var scheduleIds = bookings.Select(b => b.CourseScheduleID).Distinct().ToList();
                var userIds = bookings.Select(b => b.UserID).Distinct().ToList();

                // Fetch schedules matching the specific module ID
                var schedules = scheduleIds.Any() ? ctx.GetRepository<CourseScheduleEntity>()
                                .Find("WHERE ID IN (@0) AND ModuleId = @1", scheduleIds, moduleId)
                                .ToDictionary(s => s.ID) : new Dictionary<int, CourseScheduleEntity>();

                var planIds = schedules.Values.Select(s => s.CoursePlanID).Distinct().ToList();
                var plans = GetCoursePlansByIds(ctx, planIds); // Plans are global

                // Load users only if PortalId is valid
                var users = (userIds.Any() && currentPortalId >= 0)
                    ? userIds.Select(uid => UserController.Instance.GetUserById(currentPortalId, uid))
                          .Where(u => u != null)
                          .ToDictionary(u => u.UserID)
                    : new Dictionary<int, UserInfo>();

                foreach (var booking in bookings) {
                    // Associate schedule and plan
                    if (schedules.TryGetValue(booking.CourseScheduleID, out var schedule)) {
                        booking.CourseSchedule = schedule;
                        if (schedule != null && plans.TryGetValue(schedule.CoursePlanID, out var plan)) {
                            schedule.CoursePlan = plan;
                        }
                    } else { LogWarn($"Schedule {booking.CourseScheduleID} not found for Booking {booking.ID} in Module {moduleId}"); }

                    // Associate user info if available
                    if (users.TryGetValue(booking.UserID, out var user)) {
                        booking.UserDisplayName = user.DisplayName;
                        booking.UserEmail = user.Email;
                    }
                    else if(currentPortalId >= 0) // Only log warning if we expected to find user
                    {
                        LogWarn($"User {booking.UserID} not found for Booking {booking.ID} in Portal {currentPortalId}");
                        booking.UserDisplayName = "Unknown User";
                        booking.UserEmail = "N/A";
                    }
                }
            } catch (Exception ex) { LogError($"Error loading booking details for module {moduleId}.", ex); }
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