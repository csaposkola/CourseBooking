// calendar.js - Calendar functionality for CourseBooking module

var CourseCalendar = function (options) {
    var self = this;

    // Ensure options exist and provide defaults
    options = options || {};
    self.moduleId = options.moduleId || 0; // Default to 0 if not provided, handle appropriately server-side if needed
    self.currentYear = options.initialYear || new Date().getFullYear();
    self.currentMonth = options.initialMonth || new Date().getMonth() + 1;
    self.isAdmin = options.isAdmin || false;
    self.apiUrl = options.apiUrl || '/API/CourseBooking/CourseApi/'; // Ensure trailing slash
    self.detailsUrl = options.detailsUrl || '#'; // Provide a default or handle if missing
    self.myBookingsUrl = options.myBookingsUrl || '#'; // Provide a default
    self.userId = options.userId || 0;
    self.localTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    self.antiForgeryToken = options.antiForgeryToken; // Needed for POST operations

    // --- Initialization ---
    self.initialize = function () {
        if (!self.moduleId) {
            console.warn("CourseCalendar: ModuleID is missing or invalid. API calls might fail.");
            // Optionally display an error to the user on the page
            $('#calendarGrid').html('<tr><td colspan="7" class="text-warning">Calendar configuration error: Module ID not found.</td></tr>');
            return; // Stop initialization if module ID is missing
        }
        self.updateCalendarHeader();
        self.loadCalendarData();
        self.setupEventHandlers();
    };

    // --- Calendar UI Updates ---
    self.updateCalendarHeader = function () {
        // Use jQuery selectors safely
        $('#currentMonthDisplay').text(self.getMonthName(self.currentMonth) + ' ' + self.currentYear);
        $('#jumpToMonth').val(self.currentMonth);
        $('#jumpToYear').val(self.currentYear);
    };

    // --- Data Loading ---
    self.loadCalendarData = function () {
        // Show loading indicator
        $('#calendarGrid').html('<tr><td colspan="7" class="calendar-loading"><div class="text-center p-3"><i class="fa fa-spinner fa-spin fa-2x"></i> Loading course schedules...</div></td></tr>');

        $.ajax({
            url: self.apiUrl + 'GetCourseSchedules',
            data: {
                year: self.currentYear,
                month: self.currentMonth,
                moduleId: self.moduleId
            },
            type: 'GET',
            dataType: 'json',
            success: function (data) {
                self.renderCalendar(data);
            },
            error: function (xhr, status, error) {
                // Log detailed error information
                console.error("calendar.js: Error loading calendar data. Status:", status, "Error:", error, "XHR:", xhr);

                // Attempt to display a more specific error from the server response
                var errorMsg = "Error loading course schedules. Please check the browser console for details or try again later.";
                if (xhr && xhr.responseText) {
                    try {
                        var errorResponse = JSON.parse(xhr.responseText);
                        // Use the specific message from the DnnApiController CreateErrorResponse
                        if (errorResponse && (errorResponse.Message || errorResponse.message)) {
                            errorMsg = "Error loading calendar data: " + (errorResponse.Message || errorResponse.message);
                        }
                    } catch(e) {
                        // Response wasn't JSON, maybe HTML error page? Log snippet.
                        console.error("Raw error response (non-JSON):", xhr.responseText.substring(0, 500));
                    }
                }
                // Display error message in the calendar grid
                $('#calendarGrid').html('<tr><td colspan="7" class="text-danger"><div class="p-3">' + errorMsg + '</div></td></tr>');
            }
        });
    };

    // --- Calendar Rendering ---
    self.renderCalendar = function (schedules) {
        var firstDayOfMonth = new Date(self.currentYear, self.currentMonth - 1, 1);
        var startingDayOfWeek = firstDayOfMonth.getDay(); // 0=Sun, 1=Mon, ..., 6=Sat

        // Adjust startingDayOfWeek for Monday as first day of week (European style)
        startingDayOfWeek = (startingDayOfWeek === 0) ? 6 : startingDayOfWeek - 1; // 0=Mon, 1=Tue, ..., 6=Sun

        var daysInMonth = new Date(self.currentYear, self.currentMonth, 0).getDate();
        var daysInPrevMonth = new Date(self.currentYear, self.currentMonth - 1, 0).getDate();

        var html = '';
        var dayCounter = 1;
        var nextMonthDayCounter = 1;

        // Group schedules by day for efficient lookup
        var schedulesByDay = {};
        if (schedules && schedules.length) {
            $.each(schedules, function (i, schedule) {
                // Ensure StartTime is valid before creating Date object
                if (schedule && schedule.StartTime) {
                    try {
                        // It's usually better to parse ISO 8601 strings directly
                        var scheduleDate = new Date(schedule.StartTime);
                        if (!isNaN(scheduleDate)) { // Check if date is valid
                            var dayKey = scheduleDate.getDate();
                            if (!schedulesByDay[dayKey]) {
                                schedulesByDay[dayKey] = [];
                            }
                            schedulesByDay[dayKey].push(schedule);
                        } else {
                            console.warn("Invalid StartTime format for schedule:", schedule);
                        }
                    } catch (e) {
                        console.error("Error parsing StartTime for schedule:", schedule, e);
                    }
                }
            });
        }

        // Calculate number of rows needed (usually 5 or 6)
        var totalCells = startingDayOfWeek + daysInMonth;
        var numRows = Math.ceil(totalCells / 7);

        // Create calendar grid rows
        for (var i = 0; i < numRows; i++) {
            var rowHtml = '<tr>';

            // Create cells for each day of the week
            for (var j = 0; j < 7; j++) {
                var cellIndex = i * 7 + j;

                if (cellIndex < startingDayOfWeek) {
                    // Previous month days
                    var prevDay = daysInPrevMonth - startingDayOfWeek + j + 1;
                    rowHtml += '<td class="calendar-day empty"><div class="day-number text-muted">' + prevDay + '</div></td>';
                } else if (dayCounter > daysInMonth) {
                    // Next month days
                    rowHtml += '<td class="calendar-day empty"><div class="day-number text-muted">' + nextMonthDayCounter + '</div></td>';
                    nextMonthDayCounter++;
                } else {
                    // Current month days
                    var currentDateObj = new Date(self.currentYear, self.currentMonth - 1, dayCounter);
                    var today = new Date();
                    var isToday = dayCounter === today.getDate() && self.currentMonth === (today.getMonth() + 1) && self.currentYear === today.getFullYear();
                    // Compare date parts only for past date check
                    var isPastDate = new Date(currentDateObj.toDateString()) < new Date(today.toDateString());

                    var cellClasses = 'calendar-day';
                    if (isPastDate) cellClasses += ' past-date';
                    if (isToday) cellClasses += ' today';

                    rowHtml += '<td class="' + cellClasses + '">';
                    rowHtml += '<div class="day-number' + (isToday ? ' today-marker' : '') + '">' + dayCounter + '</div>';

                    // Add course slots for this day
                    if (schedulesByDay[dayCounter] && schedulesByDay[dayCounter].length > 0) {
                        rowHtml += '<div class="course-slots">';
                        $.each(schedulesByDay[dayCounter], function (idx, schedule) {
                            try {
                                var startTime = new Date(schedule.StartTime);
                                var formattedTime = self.formatTime(startTime);

                                var slotClass = 'time-slot';
                                var remainingSeats = schedule.AvailableSeats - (schedule.BookingCount || 0); // Default BookingCount to 0 if missing

                                // Compare dates properly
                                if (startTime < new Date()) {
                                    slotClass += ' past';
                                } else if (remainingSeats <= 0) {
                                    slotClass += ' booked';
                                } else {
                                    slotClass += ' available';
                                }

                                // Check if the user has registered for this course
                                if (self.userId > 0 && schedule.IsUserRegistered) {
                                    slotClass += ' my-booking';
                                }

                                rowHtml += '<a href="' + self.detailsUrl + '?id=' + schedule.ID + '" class="' + slotClass + '" data-modal="true" data-modal-title="' + (schedule.CoursePlan ? schedule.CoursePlan.Name : 'Course Details') + '">';
                                rowHtml += '<span class="time">' + formattedTime + '</span> - ';
                                // Ensure CoursePlan and Name exist
                                rowHtml += (schedule.CoursePlan ? schedule.CoursePlan.Name : 'Unknown Course') + ' (' + remainingSeats + '/' + schedule.AvailableSeats + ')';
                                rowHtml += '</a>';
                            } catch (e) {
                                console.error("Error rendering schedule slot:", schedule, e);
                                rowHtml += '<div class="text-danger small">Error displaying slot</div>';
                            }
                        });
                        rowHtml += '</div>'; // End course-slots
                    } else {
                        rowHtml += '<div class="course-slots no-slots"></div>'; // Placeholder for consistent height
                    }

                    rowHtml += '</td>'; // End calendar-day cell
                    dayCounter++;
                }
            } // End week day loop (j)

            rowHtml += '</tr>';
            html += rowHtml;

        } // End row loop (i)

        $('#calendarGrid').html(html);

        // Initialize AJAX/modal handlers for the slots
        if (window.CourseBookingAjax) {
            CourseBookingAjax.initializeFormHandlers($('#calendarGrid'));
        }
    };

    // --- Event Handlers ---
    self.setupEventHandlers = function () {
        // Previous month button
        $('#prevMonth').off('click').on('click', function (e) {
            e.preventDefault();
            self.navigateMonth(-1);
        });

        // Next month button
        $('#nextMonth').off('click').on('click', function (e) {
            e.preventDefault();
            self.navigateMonth(1);
        });

        // Jump to date button
        $('#jumpToDate').off('click').on('click', function (e) {
            e.preventDefault();
            var selectedMonth = parseInt($('#jumpToMonth').val());
            var selectedYear = parseInt($('#jumpToYear').val());
            // Basic validation
            if (!isNaN(selectedMonth) && selectedMonth >= 1 && selectedMonth <= 12 &&
                !isNaN(selectedYear) && selectedYear > 1900 && selectedYear < 2100) {
                self.currentMonth = selectedMonth;
                self.currentYear = selectedYear;
                self.updateCalendarHeader();
                self.loadCalendarData();
            } else {
                alert("Please select a valid month and year.");
            }
        });

        // View my bookings button (ensure URL is valid)
        if (self.myBookingsUrl && self.myBookingsUrl !== '#') {
            $('#viewMyBookings').off('click').on('click', function (e) {
                e.preventDefault();

                if (window.CourseBookingAjax) {
                    CourseBookingAjax.openModalUrl(self.myBookingsUrl, 'Foglalásaim', {
                        width: 900,
                        height: 600
                    });
                } else {
                    window.location.href = self.myBookingsUrl;
                }
            });
        } else {
            $('#viewMyBookings').hide(); // Hide if no valid URL
        }
    };

    // --- Navigation Logic ---
    self.navigateMonth = function (change) {
        self.currentMonth += change;

        if (self.currentMonth > 12) {
            self.currentMonth = 1;
            self.currentYear++;
        } else if (self.currentMonth < 1) {
            self.currentMonth = 12;
            self.currentYear--;
        }

        self.updateCalendarHeader();
        self.loadCalendarData();
    };

    // --- Helper Functions ---
    self.getMonthName = function (monthNum) {
        // Consider using locale-aware date formatting if supporting multiple languages
        var months = ['January', 'February', 'March', 'April', 'May', 'June',
            'July', 'August', 'September', 'October', 'November', 'December'];
        // Hungarian Months
        // var months = ['Január', 'Február', 'Március', 'Április', 'Május', 'Június',
        //    'Július', 'Augusztus', 'Szeptember', 'Október', 'November', 'December'];
        return months[monthNum - 1] || 'Invalid Month';
    };

    self.formatTime = function (date) {
        // Ensure date is a valid Date object
        if (!(date instanceof Date) || isNaN(date)) {
            return '??:??';
        }
        // Use padStart for cleaner formatting
        var hours = date.getHours().toString().padStart(2, '0');
        var minutes = date.getMinutes().toString().padStart(2, '0');
        return hours + ':' + minutes;
    };

    // Placeholder - Real implementation would require fetching user's bookings
    self.isUserRegistered = function(scheduleId, userId) {
        console.warn("isUserRegistered check is not implemented client-side.");
        // This check should ideally happen server-side or by loading user bookings separately.
        return false;
    };
};