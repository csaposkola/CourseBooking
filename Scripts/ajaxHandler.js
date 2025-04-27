// ajaxHandler.js - Handles AJAX form submissions and modal dialogs for CourseBooking module

var CourseBookingAjax = (function () {
    var sf; // ServiceFramework instance
    var moduleId;
    var tabId;

    function initialize(settings) {
        moduleId = settings.moduleId;
        tabId = settings.tabId;
        sf = $.ServicesFramework(moduleId);
    }

    // Opens a URL in a modal dialog
    function openModalUrl(url, title, options) {
        options = options || {};
        var width = options.width || 800;
        var height = options.height || 550;
        var showClose = options.showClose !== false;

        // Define global closeModal function for use by child frames
        window.closeModal = function() {
            var $modal = $('#courseBookingModal');
            if ($modal.hasClass('ui-dialog-content')) {
                $modal.dialog('close');
            }
        };

        var dialogOptions = {
            modal: true,
            title: title,
            width: width,
            height: height,
            resizable: false,
            closeOnEscape: true,
            close: options.onClose || function() {},
            // Add these options for better modal appearance
            closeText: "×",
            dialogClass: "dnnFormPopup courseBookingModalDialog",
            position: { my: "center", at: "center", of: window }
        };

        // Create modal container if it doesn't exist
        var $modal = $('#courseBookingModal');
        if ($modal.length === 0) {
            $modal = $('<div id="courseBookingModal"></div>').appendTo('body');
        } else if ($modal.hasClass('ui-dialog-content')) {
            // If dialog is already open, close it first
            $modal.dialog('close');
        }

        // Show loading indicator
        $modal.html('<div class="text-center p-4"><i class="fa fa-spinner fa-spin fa-3x"></i><p>Betöltés...</p></div>');
        $modal.dialog(dialogOptions);

        // Check if URL already has query parameters
        var separator = url.indexOf('?') !== -1 ? '&' : '?';

        // Add a parameter to indicate this is an AJAX request
        url = url + separator + 'isAjax=true';

        // Load content via AJAX
        $.ajax({
            url: url,
            type: 'GET',
            headers: {
                'ModuleId': moduleId,
                'TabId': tabId
            },
            success: function(data) {
                console.log("Modal content loaded successfully");

                // Instead of putting the entire response in the modal,
                // try to extract only the modal content
                var $content;

                try {
                    // Create a temporary div to parse the response
                    var $temp = $('<div></div>').html(data);

                    // Try to find the main content container
                    $content = $temp.find('.course-booking-detail, .booking-confirmation, .user-bookings');

                    if ($content.length > 0) {
                        // Found specific content container
                        console.log("Found specific content container");
                        $modal.html($content);
                    } else {
                        // Fallback: look for module container
                        $content = $temp.find('[id^="mvcContainer-"]');
                        if ($content.length > 0) {
                            console.log("Found module container");
                            $modal.html($content.html());
                        } else {
                            // Last resort: use the whole response
                            console.log("Using full response");
                            $modal.html(data);
                        }
                    }
                } catch (e) {
                    console.error("Error parsing modal content:", e);
                    // Fallback to the original response
                    $modal.html(data);
                }

                // Ensure the modal is properly sized after content is loaded
                $modal.dialog("option", "position", { my: "center", at: "center", of: window });

                // Initialize form handlers in the modal
                initializeFormHandlers($modal);
            },
            error: function(xhr, status, error) {
                console.error("Error loading modal content:", error);
                $modal.html('<div class="alert alert-danger">Hiba történt a tartalom betöltése közben: ' + error + '</div>');
            }
        });
    }

    // Process form submission via AJAX
    function submitFormAjax(form, successCallback, errorCallback) {
        var $form = $(form);
        var url = $form.attr('action');
        var method = $form.attr('method') || 'POST';

        $.ajax({
            url: url,
            type: method,
            data: $form.serialize(),
            headers: {
                'ModuleId': moduleId,
                'TabId': tabId,
                'RequestVerificationToken': sf.getAntiForgeryValue()
            },
            success: function(response) {
                if (successCallback) {
                    successCallback(response);
                }
            },
            error: function(xhr, status, error) {
                if (errorCallback) {
                    errorCallback(xhr, status, error);
                } else {
                    alert('An error occurred: ' + error);
                }
            }
        });
    }

    // Initialize form handlers inside a container
    function initializeFormHandlers(container) {
        // Handle forms with data-ajax="true" attribute
        $(container).find('form[data-ajax="true"]').on('submit', function(e) {
            e.preventDefault();

            var $form = $(this);
            var successUrl = $form.data('success-url');
            var successCallback = $form.data('success-callback');

            submitFormAjax(this, function(response) {
                if (successCallback && window[successCallback]) {
                    // Call a named function if specified
                    window[successCallback](response);
                } else if (successUrl) {
                    // Redirect to success URL if specified
                    window.location.href = successUrl;
                } else {
                    // Default: reload calendar data
                    if (window.courseCalendar) {
                        window.courseCalendar.loadCalendarData();
                    }

                    // Close modal if open
                    if ($('#courseBookingModal').dialog('instance')) {
                        $('#courseBookingModal').dialog('close');
                    }
                }
            });
        });

        // Handle links with data-modal="true" attribute
        $(container).find('a[data-modal="true"]').on('click', function(e) {
            e.preventDefault();
            var url = $(this).attr('href');
            var title = $(this).data('modal-title') || 'Details';
            var width = $(this).data('modal-width') || 800;
            var height = $(this).data('modal-height') || 550;

            openModalUrl(url, title, {
                width: width,
                height: height
            });
        });
    }

    // Public API
    return {
        initialize: initialize,
        openModalUrl: openModalUrl,
        submitFormAjax: submitFormAjax,
        initializeFormHandlers: initializeFormHandlers
    };
})();

// Initialize when document is ready
$(document).ready(function() {
    // Will be initialized by specific views
});