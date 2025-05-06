// Scripts/Calendar.js
function initializeCalendar(moduleId, apiUrl) {
    console.log("Initializing calendar for module: " + moduleId);
    var calendarEl = document.getElementById('calendar-' + moduleId);
    var btnMonthView = document.getElementById('btnMonthView-' + moduleId);
    var btnWeekView = document.getElementById('btnWeekView-' + moduleId);

    if (!calendarEl) {
        console.error("Calendar element not found!");
        return;
    }

    // Format price as HUF
    function formatPrice(price) {
        return new Intl.NumberFormat('hu-HU', {
            style: 'currency',
            currency: 'HUF',
            minimumFractionDigits: 0
        }).format(price);
    }

    // Initialize FullCalendar
    try {
        console.log("Creating calendar instance");
        var calendar = new FullCalendar.Calendar(calendarEl, {
            initialView: 'dayGridMonth',
            headerToolbar: {
                left: 'prev,next today',
                center: 'title',
                right: ''
            },
            locale: 'hu',
            timeZone: 'local',
            firstDay: 1, // Monday as first day
            eventTimeFormat: {
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            },
            displayEventTime: true,
            eventClick: function (info) {
                // Prevent default navigation
                info.jsEvent.preventDefault();

                // Get the product SKU from the event data
                // Assuming the SKU is available in the event object, if not, you might need to extract it from somewhere else
                var productSku = info.event.extendedProps.sku || ""; // Fallback to empty string if not available

                if (!productSku) {
                    console.error("Product SKU not found in event data");
                    return false;
                }

                // Show loading indicator
                var $loadingIndicator = $('<div class="loading-indicator">Kosárba helyezés...</div>');
                $(info.el).append($loadingIndicator);

                // Create the cart URL with the correct format
                var cartUrl = "http://rendfejl1008.northeurope.cloudapp.azure.com:8080/HotcakesStore/Cart?QuickAddSku=" + productSku + "&QuickAddQty=1";

                // Redirect after a short delay to show the loading indicator
                setTimeout(function() {
                    window.location.href = cartUrl;
                }, 500);

                return false;
            },
            eventDidMount: function (info) {
                // Add tooltip with additional information
                $(info.el).popover({
                    title: info.event.title,
                    content:
                        '<div class="event-popover-content">' +
                        '<div><strong>Ár:</strong> ' + info.event.extendedProps.price + '</div>' +
                        '<div><strong>Időtartam:</strong> ' + info.event.extendedProps.duration + '</div>' +
                        '<div><strong>Szabad helyek:</strong> ' + info.event.extendedProps.inventory + '</div>' +
                        '</div>',
                    placement: 'top',
                    trigger: 'hover',
                    container: 'body',
                    html: true
                });
            },
            events: function (info, successCallback, failureCallback) {
                console.log("Fetching course events from: " + apiUrl);
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
                        console.log("API Response successful:", data);
                        var events = [];

                        try {
                            // Convert the course data to FullCalendar events
                            $.each(data, function (i, item) {
                                console.log("Processing item:", item);

                                // Safely parse date
                                var startDate;
                                try {
                                    startDate = new Date(item.StartDate);
                                    console.log("Parsed start date:", startDate);

                                    if (isNaN(startDate.getTime())) {
                                        console.error("Invalid date parsed:", item.StartDate);
                                        return; // Skip this item
                                    }
                                } catch (e) {
                                    console.error("Error parsing date:", e);
                                    return; // Skip this item
                                }

                                // Calculate the end date
                                var endDate = new Date(startDate.getTime() + (item.DurationHours * 60 * 60 * 1000));

                                // Create the full URL
                                var courseUrl = "http://rendfejl1008.northeurope.cloudapp.azure.com:8080/Termékek/" + item.ProductLink;

                                var eventObject = {
                                    id: item.ProductId,
                                    title: item.ProductName,
                                    start: startDate,
                                    end: endDate,
                                    url: courseUrl,
                                    extendedProps: {
                                        price: formatPrice(item.SitePrice),
                                        inventory: item.InventoryCount,
                                        duration: item.DurationHours + ' óra',
                                        sku: item.Sku // Save the SKU for use in the cart URL
                                    }
                                };

                                console.log("Created event object:", eventObject);
                                events.push(eventObject);
                            });
                        } catch (error) {
                            console.error("Error processing course data:", error);
                        }

                        console.log("Processed events:", events);
                        if (events.length === 0) {
                            console.warn("No events were created!");
                        }

                        successCallback(events);
                    },
                    error: function (xhr, status, error) {
                        console.error("Error fetching courses: " + error);
                        console.error("Status:", status);
                        console.error("Response:", xhr.responseText);
                        // Return empty array to avoid calendar errors
                        successCallback([]);
                    }
                });
            }
        });

        console.log("Rendering calendar");
        calendar.render();
        console.log("Calendar rendered");

        // Add event listeners to buttons
        if (btnMonthView) {
            btnMonthView.addEventListener('click', function (e) {
                e.preventDefault();
                console.log("Switching to month view");
                calendar.changeView('dayGridMonth');

                btnMonthView.classList.add('active');
                btnMonthView.classList.remove('btn-default');
                btnMonthView.classList.add('btn-primary');

                btnWeekView.classList.remove('active');
                btnWeekView.classList.remove('btn-primary');
                btnWeekView.classList.add('btn-default');
            });
        } else {
            console.error("Month view button not found!");
        }

        if (btnWeekView) {
            btnWeekView.addEventListener('click', function (e) {
                e.preventDefault();
                console.log("Switching to week view");
                calendar.changeView('timeGridWeek');

                btnWeekView.classList.add('active');
                btnWeekView.classList.remove('btn-default');
                btnWeekView.classList.add('btn-primary');

                btnMonthView.classList.remove('active');
                btnMonthView.classList.remove('btn-primary');
                btnMonthView.classList.add('btn-default');
            });
        } else {
            console.error("Week view button not found!");
        }
    } catch (error) {
        console.error("Error initializing calendar:", error);
    }
}