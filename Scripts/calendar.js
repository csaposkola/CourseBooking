var CourseCalendar = function (options) {
    var self = this;

    self.moduleId = options.moduleId;
    self.currentYear = options.initialYear || new Date().getFullYear();
    self.currentMonth = options.initialMonth || new Date().getMonth() + 1;
    self.isAdmin = options.isAdmin || false;
    self.apiUrl = options.apiUrl;
    self.createUrl = options.createUrl;
    self.detailUrl = options.detailUrl;

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
        $.ajax({
            url: self.apiUrl + 'Bookings_List',
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
                $('#calendarGrid').html('<tr><td colspan="7">Még nem készült el az itt megjelenő grid.</td></tr>');
            }
        });
    };

    self.renderCalendar = function (bookings) {
        var firstDay = new Date(self.currentYear, self.currentMonth - 1, 1);
        var startingDay = firstDay.getDay() || 7;
        var monthLength = new Date(self.currentYear, self.currentMonth, 0).getDate();
        var html = '';

        // Calendar grid rendering code would go here

        $('#calendarGrid').html(html);
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
};