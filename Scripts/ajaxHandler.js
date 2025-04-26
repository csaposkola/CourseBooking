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

        var dialogOptions = {
            modal: true,
            title: title,
            width: width,
            height: height,
            resizable: false,
            close: options.onClose || function() {}
        };

        // Create modal container if it doesn't exist
        var $modal = $('#courseBookingModal');
        if ($modal.length === 0) {
            $modal = $('<div id="courseBookingModal"></div>').appendTo('body');
        }

        // Show loading indicator
        $modal.html('<div class="text-center p-4"><i class="fa fa-spinner fa-spin fa-3x"></i><p>Loading...</p></div>');
        $modal.dialog(dialogOptions);

        // Load content via AJAX
        $.ajax({
            url: url,
            type: 'GET',
            headers: {
                'ModuleId': moduleId,
                'TabId': tabId
            },
            success: function(data) {
                $modal.html(data);

                // Initialize form handlers in the modal
                initializeFormHandlers($modal);
            },
            error: function(xhr, status, error) {
                $modal.html('<div class="alert alert-danger">Error loading content: ' + error + '</div>');
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
            var width = $(this).data('modal-width');
            var height = $(this).data('modal-height');

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