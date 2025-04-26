var CourseCalendar = function (options) {
    var self = this;

    self.moduleId = options.moduleId;
    self.currentYear = options.initialYear || new Date().getFullYear();
    self.currentMonth = options.initialMonth || new Date().getMonth() + 1;
    self.isAdmin = options.isAdmin || false;
    self.apiUrl = options.apiUrl;
    self.detailsUrl = options.detailsUrl;
    self.myBookingsUrl = options.myBookingsUrl;
    self.userId = options.userId || 0;
    self.localTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    self.antiForgeryToken = options.antiForgeryToken;

    self.initialize = function () {
        self.updateCalendarHeader();
        self.loadCalendarData();
        self.setupEventHandlers();
    };

    self.updateCalendarHeader = function () {
        $('#currentMonthDisplay').text(self.getMonthName(self.currentMonth) + ' ' + self.currentYear);
        $('#jumpToMonth').val(self.currentMonth);
        $('#jumpToYear').val(self.currentYear);
    };

    self.loadCalendarData = function () {
        $('#calendarGrid').html('<tr><td colspan="7" class="calendar-loading"><i class="fa fa-spinner fa-spin"></i> Loading course schedules...</td></tr>');

        $.ajax({
            url: self.apiUrl + 'GetCourseSchedules',
            data: { year: self.currentYear, month: self.currentMonth },
            type: 'GET',
            beforeSend: function (xhr) {
                xhr.setRequestHeader("ModuleId", self.moduleId);
            },
            success: function (data) {
                self.renderCalendar(data);
            },
            error: function (xhr, status, error) {
                console.error("Error loading calendar data:", error);
                $('#calendarGrid').html('<tr><td colspan="7" class="text-danger">Error loading course schedules. Please try again later.</td></tr>');
            }
        });
    };

    self.renderCalendar = function (schedules) {
        var firstDay = new Date(self.currentYear, self.currentMonth - 1, 1);
        var startingDay = firstDay.getDay();
        if (startingDay === 0) startingDay = 7; // Convert Sunday (0) to be 7 for easier calculation

        var monthLength = new Date(self.currentYear, self.currentMonth, 0).getDate();
        var prevMonthLength = new Date(self.currentYear, self.currentMonth - 1, 0).getDate();

        var html = '';
        var day = 1;
        var nextMonthDay = 1;

        // Group schedules by day
        var schedulesByDay = {};
        if (schedules && schedules.length) {
            $.each(schedules, function (i, schedule) {
                var scheduleDate = new Date(schedule.StartTime);
                var dayKey = scheduleDate.getDate();

                if (!schedulesByDay[dayKey]) {
                    schedulesByDay[dayKey] = [];
                }
                schedulesByDay[dayKey].push(schedule);
            });
        }

        // Create calendar grid
        for (var i = 0; i < 6; i++) {
            var rowHtml = '<tr>';

            for (var j = 1; j <= 7; j++) {
                if (i === 0 && j < startingDay) {
                    // Previous month days
                    var prevDay = prevMonthLength - startingDay + j + 1;
                    rowHtml += '<td class="calendar-day empty"><div class="day-number text-muted">' + prevDay + '</div></td>';
                }
                else if (day > monthLength) {
                    // Next month days
                    rowHtml += '<td class="calendar-day empty"><div class="day-number text-muted">' + nextMonthDay + '</div></td>';
                    nextMonthDay++;
                }
                else {
                    // Current month days
                    var today = new Date();
                    var isToday = day === today.getDate() && self.currentMonth === (today.getMonth() + 1) && self.currentYear === today.getFullYear();
                    var isPastDate = new Date(self.currentYear, self.currentMonth - 1, day) < new Date().setHours(0, 0, 0, 0);

                    rowHtml += '<td class="calendar-day' + (isPastDate ? ' past-date' : '') + (isToday ? ' today' : '') + '">';
                    rowHtml += '<div class="day-number' + (isToday ? ' today-marker' : '') + '">' + day + '</div>';

                    // Add time slots for this day
                    if (schedulesByDay[day] && schedulesByDay[day].length > 0) {
                        rowHtml += '<div class="course-slots">';

                        $.each(schedulesByDay[day], function (i, schedule) {
                            var startTime = new Date(schedule.StartTime);
                            var localStartTime = new Date(startTime.toLocaleString('en-US', { timeZone: self.localTimeZone }));
                            var formattedTime = self.formatTime(localStartTime);

                            var slotClass = 'time-slot';
                            var remainingSeats = schedule.AvailableSeats - schedule.BookingCount;

                            if (schedule.StartTime < new Date().toISOString()) {
                                slotClass += ' past';
                            } else if (self.userId > 0 && self.isUserRegistered(schedule, self.userId)) {
                                slotClass += ' my-booking';
                            } else if (remainingSeats <= 0) {
                                slotClass += ' booked';
                            } else {
                                slotClass += ' available';
                            }

                            rowHtml += '<a href="' + self.detailsUrl + '?id=' + schedule.ID + '" class="' + slotClass + '">';
                            rowHtml += '<span class="time">' + formattedTime + '</span> - ';
                            rowHtml += schedule.CoursePlan.Name + ' (' + remainingSeats + '/' + schedule.AvailableSeats + ')';
                            rowHtml += '</a>';
                        });

                        rowHtml += '</div>';
                    }

                    rowHtml += '</td>';
                    day++;
                }
            }

            rowHtml += '</tr>';
            html += rowHtml;

            // Stop rendering if we're already past the month
            if (day > monthLength) {
                break;
            }
        }

        $('#calendarGrid').html(html);
    };

    self.isUserRegistered = function(schedule, userId) {
        // Simplified check - in a real implementation this would need to be checked server-side
        return false;
    };

    self.setupEventHandlers = function () {
        $('#prevMonth').click(function () {
            self.navigateMonth(-1);
        });

        $('#nextMonth').click(function () {
            self.navigateMonth(1);
        });

        $('#jumpToDate').click(function () {
            self.currentMonth = parseInt($('#jumpToMonth').val());
            self.currentYear = parseInt($('#jumpToYear').val());
            self.updateCalendarHeader();
            self.loadCalendarData();
        });

        // View my bookings
        $('#viewMyBookings').click(function () {
            window.location.href = self.myBookingsUrl;
        });
    };

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

    self.getMonthName = function (monthNum) {
        var months = ['Január', 'Február', 'Március', 'Április', 'Május', 'Június',
            'Július', 'Augusztus', 'Szeptember', 'Október', 'November', 'December'];
        return months[monthNum - 1];
    };

    self.formatTime = function (date) {
        var hours = date.getHours().toString().padStart(2, '0');
        var minutes = date.getMinutes().toString().padStart(2, '0');
        return hours + ':' + minutes;
    };
};