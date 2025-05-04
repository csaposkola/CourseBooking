// Scripts/Calendar.js
function initializeCalendar(moduleId, apiUrl) {
    var calendarEl = document.getElementById('calendar-' + moduleId);
    var btnMonthView = document.getElementById('btnMonthView-' + moduleId);
    var btnWeekView = document.getElementById('btnWeekView-' + moduleId);

    // Format price as HUF
    function formatPrice(price) {
        return new Intl.NumberFormat('hu-HU', {
            style: 'currency',
            currency: 'HUF',
            minimumFractionDigits: 0
        }).format(price);
    }

    // Get course data from API
    function fetchCourseEvents(successCallback) {
        $.ajax({
            url: apiUrl,
            type: 'GET',
            dataType: 'json',
            headers: {
                "ModuleId": moduleId
            },
            beforeSend: function (xhr) {
                var token = $('input[name="__RequestVerificationToken"]').val();
                if (token) {
                    xhr.setRequestHeader("__RequestVerificationToken", token);
                }
            },
            success: function (data) {
                var events = [];

                // Convert the course data to FullCalendar events
                $.each(data, function (i, item) {
                    events.push({
                        id: item.ProductId,
                        title: item.ProductName,
                        start: new Date(parseInt(item.StartDate.substr(6))),
                        end: new Date(parseInt(item.StartDate.substr(6)) + (item.DurationHours * 60 * 60 * 1000)),
                        url: window.location.origin + '/' + item.ProductLink,
                        extendedProps: {
                            price: formatPrice(item.SitePrice),
                            inventory: item.InventoryCount,
                            duration: item.DurationHours + ' óra'
                        }
                    });
                });

                successCallback(events);
            },
            error: function (xhr, status, error) {
                console.error("Error fetching courses: " + error);
            }
        });
    }

    // Initialize FullCalendar
    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: ''
        },
        locale: 'hu',
        timeZone: 'Europe/Budapest',
        firstDay: 1, // Monday as first day
        eventTimeFormat: {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        },
        eventClick: function(info) {
            if (info.event.url) {
                window.location.href = info.event.url;
                return false;
            }
        },
        eventDidMount: function(info) {
            // Add tooltip with additional information
            $(info.el).popover({
                title: info.event.title,
                content:
                    '<div><strong>Ár:</strong> ' + info.event.extendedProps.price + '</div>' +
                    '<div><strong>Időtartam:</strong> ' + info.event.extendedProps.duration + '</div>' +
                    '<div><strong>Szabad helyek:</strong> ' + info.event.extendedProps.inventory + '</div>',
                placement: 'top',
                trigger: 'hover',
                container: 'body',
                html: true
            });
        },
        events: function(fetchInfo, successCallback, failureCallback) {
            fetchCourseEvents(successCallback);
        }
    });

    calendar.render();

    // Switch between month and week views
    btnMonthView.addEventListener('click', function() {
        calendar.changeView('dayGridMonth');
        btnMonthView.classList.add('active');
        btnMonthView.classList.remove('btn-default');
        btnMonthView.classList.add('btn-primary');
        btnWeekView.classList.remove('active');
        btnWeekView.classList.remove('btn-primary');
        btnWeekView.classList.add('btn-default');
    });

    btnWeekView.addEventListener('click', function() {
        calendar.changeView('timeGridWeek');
        btnWeekView.classList.add('active');
        btnWeekView.classList.remove('btn-default');
        btnWeekView.classList.add('btn-primary');
        btnMonthView.classList.remove('active');
        btnMonthView.classList.remove('btn-primary');
        btnMonthView.classList.add('btn-default');
    });
}