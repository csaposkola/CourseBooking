// ajaxHandler.js - AJAX handling utilities for CourseBooking module

var CourseBookingAjax = (function() {
    'use strict';

    // Private module variables
    var _config = {
        moduleId: 0,
        tabId: 0,
        antiForgeryToken: ''
    };

    var _modalId = 'courseBookingModal';
    var _modalDialogClass = 'courseBookingModalDialog';

    // Initialize the module with configuration
    function initialize(config) {
        _config.moduleId = config.moduleId || 0;
        _config.tabId = config.tabId || 0;

        // Get antiForgeryToken if not provided
        if (!_config.antiForgeryToken) {
            var sf = $.ServicesFramework(_config.moduleId);
            _config.antiForgeryToken = sf.getAntiForgeryValue();
        }

        // Initialize the modal container if it doesn't exist
        if ($('#' + _modalId).length === 0) {
            $('body').append('<div id="' + _modalId + '" style="display:none;"></div>');
        }
    }

    // Open a URL in a modal dialog
    function openModalUrl(url, title, options) {
        // Ensure URL contains isAjax=true parameter
        url = url + (url.indexOf('?') > -1 ? '&' : '?') + 'isAjax=true';

        // Default options
        options = options || {};
        var modalOptions = {
            width: options.width || 800,
            height: options.height || 600,
            title: title || 'Details',
            modal: true,
            dialogClass: _modalDialogClass,
            close: function() {
                $(this).dialog('destroy');
                $(this).html('');
            }
        };

        // Show loading indicator
        $('#' + _modalId).html('<div class="text-center p-4"><i class="fa fa-spinner fa-spin fa-2x"></i><br>Loading...</div>');

        // Open dialog immediately with loading indicator
        $('#' + _modalId).dialog(modalOptions);

        // Load content
        $.ajax({
            url: url,
            type: 'GET',
            success: function(data) {
                $('#' + _modalId).html(data);

                // Re-initialize form handlers in the modal
                initializeFormHandlers($('#' + _modalId));

                // Adjust dialog height if content is smaller than expected
                var contentHeight = $('#' + _modalId).prop('scrollHeight');
                if (contentHeight < options.height) {
                    $('#' + _modalId).dialog('option', 'height', 'auto');
                }

                // Ensure close button is visible and works
                $('.modal-close').on('click', function() {
                    closeModal();
                });
            },
            error: function(xhr, status, error) {
                $('#' + _modalId).html(
                    '<div class="alert alert-danger">' +
                    '<h4>Error Loading Content</h4>' +
                    '<p>' + (xhr.responseText || error || 'Unknown error occurred.') + '</p>' +
                    '<button type="button" class="btn btn-default modal-close">Close</button>' +
                    '</div>'
                );

                $('.modal-close').on('click', function() {
                    closeModal();
                });
            }
        });
    }

    // Close the modal dialog
    function closeModal() {
        if ($('#' + _modalId).dialog('instance')) {
            $('#' + _modalId).dialog('close');
        }
    }

    // Process a form with AJAX
    function submitFormAjax(form) {
        var $form = $(form);
        var formData = $form.serialize();
        var url = $form.attr('action');
        var method = $form.attr('method') || 'POST';
        var successCallback = window[$form.data('success-callback')];
        var errorCallback = window[$form.data('error-callback')];

        // Disable form submission button to prevent double submits
        $form.find('button[type="submit"]').prop('disabled', true);

        // Add loading indicator
        var $submitBtn = $form.find('button[type="submit"]');
        var originalBtnHtml = $submitBtn.html();
        $submitBtn.html('<i class="fa fa-spinner fa-spin"></i> ' + originalBtnHtml);

        // Perform AJAX request
        $.ajax({
            url: url,
            type: method,
            data: formData,
            beforeSend: function(xhr) {
                xhr.setRequestHeader("ModuleId", _config.moduleId);
                xhr.setRequestHeader("TabId", _config.tabId);
                if (method.toUpperCase() === 'POST') {
                    xhr.setRequestHeader('RequestVerificationToken', _config.antiForgeryToken);
                }
            },
            success: function(response) {
                // Reset button state
                $form.find('button[type="submit"]').prop('disabled', false);
                $submitBtn.html(originalBtnHtml);

                // Call success callback if provided
                if (typeof successCallback === 'function') {
                    successCallback(response);
                } else {
                    // Default behavior - show success message and close modal if in one
                    alert('Operation completed successfully.');
                    closeModal();
                }
            },
            error: function(xhr, status, error) {
                // Reset button state
                $form.find('button[type="submit"]').prop('disabled', false);
                $submitBtn.html(originalBtnHtml);

                // Call error callback if provided
                if (typeof errorCallback === 'function') {
                    errorCallback(xhr, status, error);
                } else {
                    // Default error handling
                    var errorMsg = 'Error processing request.';
                    try {
                        var response = JSON.parse(xhr.responseText);
                        if (response && (response.Message || response.message)) {
                            errorMsg = response.Message || response.message;
                        }
                    } catch (e) {}

                    alert(errorMsg);
                }
            }
        });
    }

    // Initialize form handlers in a container
    function initializeFormHandlers(container) {
        // Find forms with data-ajax="true" attribute
        $(container).find('form[data-ajax="true"]').off('submit').on('submit', function(e) {
            e.preventDefault();
            submitFormAjax(this);
        });

        // Initialize modal links
        $(container).find('a[data-modal="true"]').off('click').on('click', function(e) {
            e.preventDefault();
            var url = $(this).attr('href');
            var title = $(this).data('modal-title') || $(this).attr('title') || 'Details';
            var width = $(this).data('modal-width') || 800;
            var height = $(this).data('modal-height') || 600;

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
        closeModal: closeModal,
        submitFormAjax: submitFormAjax,
        initializeFormHandlers: initializeFormHandlers
    };
})();

// Initialize when jQuery is ready (if using outside of DNN context)
$(document).ready(function() {
    // Don't do anything here - initialization must be called explicitly
    // after DNN ServicesFramework is available
});