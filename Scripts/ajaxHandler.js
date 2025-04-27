// ajaxHandler.js - Handles AJAX form submissions and modal dialogs for CourseBooking module

var CourseBookingAjax = (function () {
    var sf; // ServiceFramework instance
    var moduleId;
    var tabId;
    var currentModal = null; // Keep track of the current modal instance

    // Initialize with DNN context (ModuleID, TabID)
    function initialize(settings) {
        moduleId = settings.moduleId;
        tabId = settings.tabId;
        if (!moduleId) {
            console.error("CourseBookingAjax: ModuleId is missing during initialization.");
            return;
        }
        // Initialize DNN Services Framework
        sf = $.ServicesFramework(moduleId);
        console.log("CourseBookingAjax initialized for ModuleId:", moduleId, "TabId:", tabId);
    }

    // Internal function to close the currently tracked modal
    function closeModalInternal() {
        var $modalToClose = null;
        if (currentModal && currentModal.length > 0 && currentModal.hasClass('ui-dialog-content') && currentModal.dialog('instance')) {
            $modalToClose = currentModal;
            console.log("Closing modal via internal reference:", $modalToClose.attr('id'));
        } else {
            // Fallback if internal reference is lost or invalid, try finding by common ID pattern
            $modalToClose = $('[id^="courseBookingModal_"]:visible').last();
            if ($modalToClose.length > 0 && $modalToClose.hasClass('ui-dialog-content') && $modalToClose.dialog('instance')) {
                console.log("Closing modal via ID lookup:", $modalToClose.attr('id'));
            } else {
                $modalToClose = null; // Not found or not a dialog
            }
        }

        if ($modalToClose) {
            try {
                $modalToClose.dialog('close'); // Trigger the close event which handles cleanup
            } catch (e) {
                console.error("Error closing dialog:", e);
                // Force removal if 'close' fails
                $modalToClose.remove();
                currentModal = null;
            }
        } else {
            console.warn("Could not find a modal dialog to close.");
        }
    }

    // Expose closeModal on the window object for iframe access (if needed)
    // Calling window.parent.closeModal() from iframe is the typical use case
    window.closeModal = closeModalInternal;

    // Opens a URL in a jQuery UI modal dialog
    function openModalUrl(url, title, options) {
        options = options || {};
        var width = options.width || 800;
        var height = options.height || 600; // Adjusted default height
        var showCloseButton = options.showClose !== false; // Default to true

        // Ensure any previously tracked modal is closed before opening a new one
        if (currentModal) {
            console.log("Closing previous modal before opening new one.");
            closeModalInternal(); // Use the internal closer
        }

        // Create a *new* modal container div with a unique ID each time
        var modalId = 'courseBookingModal_' + Date.now();
        var $modal = $('<div id="' + modalId + '" class="course-booking-modal-content"></div>').appendTo('body');
        currentModal = $modal; // Store reference to the *new* modal

        console.log("Creating modal with ID:", modalId, "for URL:", url);
        $modal.html('<div class="text-center p-4" style="padding: 20px;"><i class="fa fa-spinner fa-spin fa-3x"></i><p style="margin-top: 10px;">Betöltés...</p></div>');

        var dialogOptions = {
            modal: true,
            title: title,
            width: width,
            height: height,
            resizable: false,
            closeOnEscape: true,
            draggable: false, // Usually better for app-like modals
            classes: { // Use jQuery UI 1.12+ classes structure
                "ui-dialog": "dnnFormPopup courseBookingModalDialog", // Add custom class to the outer wrapper
                "ui-dialog-titlebar": "dnnPopup_titlebar", // Standard DNN titlebar class
                "ui-dialog-content": "dnnPopup_content"   // Standard DNN content class
            },
            closeText: "Bezárás", // Accessibility text for close button
            open: function(event, ui) {
                // Ensure it's positioned correctly after opening
                $(this).dialog('option', 'position', { my: 'center', at: 'center', of: window });
            },
            close: function(event, ui) {
                console.log("Dialog close event triggered for:", $(this).attr('id'));
                // Clean up: destroy the dialog widget and remove the element from the DOM
                try {
                    $(this).dialog('destroy').remove();
                } catch(e) {
                    console.warn("Error destroying dialog:", e);
                    $(this).remove(); // Force remove element if destroy fails
                }

                // Clear the reference if this was the currently tracked modal
                if (currentModal && currentModal.attr('id') === $(this).attr('id')) {
                    currentModal = null;
                    console.log("Cleared currentModal reference.");
                }
            }
            // Note: DNN themes might override some styles. The 'dialogClass' helps target specifics.
        };

        if (!showCloseButton) {
            // Hide the entire title bar's close button if requested
            dialogOptions.classes["ui-dialog-titlebar-close"] = "ui-helper-hidden-accessible";
        }

        // Initialize the dialog
        $modal.dialog(dialogOptions);

        // Ensure the close button in the title bar is styled reasonably (DNN themes might interfere)
        // It's better to style `.courseBookingModalDialog .ui-dialog-titlebar-close` in CSS
        //$modal.parent().find('.ui-dialog-titlebar-close')
        //     .addClass('btn btn-xs btn-default') // Example: Add Bootstrap button classes
        //     .html('<i class="fa fa-times"></i>') // Use an icon
        //     .css({ /* Minimal inline styles if absolutely necessary */ });

        // Add parameter to indicate AJAX context for the server
        var separator = url.indexOf('?') !== -1 ? '&' : '?';
        var ajaxUrl = url + separator + 'isAjax=true';
        // Add popUp=true if DNN requires it for certain operations within modal context
        if (ajaxUrl.indexOf('popUp=') === -1) {
            ajaxUrl += '&popUp=true';
        }


        // --- Load modal content via AJAX ---
        $.ajax({
            url: ajaxUrl,
            type: 'GET',
            cache: false, // Prevent caching of modal content
            headers: {
                // Pass ModuleId and TabId for DNN Services Framework context if needed by GET
                'ModuleId': moduleId,
                'TabId': tabId,
                // AntiForgeryToken usually not needed for GET, but include if server requires
                'RequestVerificationToken': sf ? sf.getAntiForgeryValue() : null
            },
            success: function(data) {
                // Check if the modal we intended to load into still exists and is the current one
                if (!currentModal || currentModal.attr('id') !== modalId || !currentModal.dialog('instance')) {
                    console.warn("Modal changed or closed before content loaded for:", modalId, ". Aborting content insertion.");
                    return;
                }
                console.log("Modal content loaded successfully for:", modalId);

                var $contentToInject;
                try {
                    // Create a temporary container to parse the response HTML
                    var $temp = $('<div></div>').html(data);

                    // --- Try to extract the specific content container ---
                    // Priority 1: Specific class for details/confirmation/list views
                    $contentToInject = $temp.find('.course-booking-detail, .booking-confirmation, .user-bookings');

                    if ($contentToInject.length === 0) {
                        // Priority 2: Fallback to finding a generic DNN module container if specific class not found
                        $contentToInject = $temp.find('[id^="dnn_ctr' + moduleId + '_ModuleContent"]');
                        if ($contentToInject.length > 0) {
                            console.log("Found DNN module container content.");
                            // Extract the *inner* HTML of the module container
                            $contentToInject = $contentToInject.children();
                        }
                    }

                    if ($contentToInject.length > 0) {
                        console.log("Using extracted content container.");
                        $modal.html($contentToInject);
                    } else {
                        // Last resort: Check if the response *looks* like partial content (e.g., has expected forms/elements)
                        // Avoid injecting full page HTML.
                        if ($temp.find('form#bookingForm').length > 0 || $temp.find('.booking-confirmation').length > 0 || $temp.find('.cancel-form').length > 0) {
                            console.warn("Could not find specific container, using full response as likely partial content.");
                            $modal.html(data);
                        } else {
                            console.error("Response does not seem to be valid modal content. Contains:", data.substring(0, 200) + "...");
                            $modal.html('<div class="alert alert-danger">Hiba: Érvénytelen tartalom érkezett a szerverről.</div>');
                        }
                    }
                } catch (e) {
                    console.error("Error parsing or injecting modal content:", e);
                    // Fallback to showing raw response (potentially unsafe) or an error message
                    $modal.html('<div class="alert alert-danger">Hiba történt a tartalom feldolgozása közben.</div>');
                }

                // Reposition the dialog after content is loaded and size might have changed
                try {
                    $modal.dialog("option", "position", { my: "center", at: "center", of: window });
                } catch(e) { console.warn("Could not reposition dialog after content load.", e); }

                // --- IMPORTANT: Initialize specific handlers for elements *within* the loaded modal content ---
                initializeFormHandlers($modal);

            },
            error: function(xhr, status, error) {
                console.error("Error loading modal content:", { url: ajaxUrl, status: status, error: error, response: xhr.responseText });
                // Check if the modal still exists before showing the error
                if (currentModal && currentModal.attr('id') === modalId && currentModal.dialog('instance')) {
                    var errorText = 'Hiba történt a tartalom betöltése közben.';
                    if (xhr.status === 404) errorText = 'A kért tartalom nem található.';
                    else if (xhr.status === 401 || xhr.status === 403) errorText = 'Nincs jogosultsága a tartalom megtekintéséhez.';
                    else errorText += ' (' + error + ')';
                    $modal.html('<div class="alert alert-danger">' + errorText + '</div>');
                } else {
                    console.warn("Modal changed or closed before error could be displayed for:", modalId);
                }
            }
        });
    }

    // Process form submission via AJAX using DNN Services Framework
    function submitFormAjax(form, successCallback, errorCallback) {
        var $form = $(form);
        var url = $form.attr('action');
        var method = $form.attr('method') || 'POST';

        // Ensure Services Framework is available
        if (!sf) {
            console.error("DNN Services Framework (sf) is not initialized. Cannot submit form.");
            alert("Hiba: A kliens oldali keretrendszer nincs megfelelően beállítva.");
            if (errorCallback) errorCallback(null, "Configuration Error", "sf not initialized");
            return;
        }

        // Disable submit button(s) to prevent double submission
        var $submitButtons = $form.find('button[type="submit"]');
        var originalButtonHtml = [];
        $submitButtons.each(function(i) {
            originalButtonHtml[i] = $(this).html();
            $(this).prop('disabled', true).html('<i class="fa fa-spinner fa-spin"></i> Feldolgozás...');
        });

        console.log("Submitting form via AJAX:", { url: url, method: method });

        $.ajax({
            url: url,
            type: method,
            data: $form.serialize(), // Send form data
            // Using DNN Services Framework headers for ModuleId, TabId, and AntiForgeryToken
            headers: sf.getAntiForgeryValue() ? { // Check if token exists
                'ModuleId': moduleId,
                'TabId': tabId,
                'RequestVerificationToken': sf.getAntiForgeryValue()
            } : { // Fallback if token somehow missing (less secure)
                'ModuleId': moduleId,
                'TabId': tabId
            },
            success: function(response) {
                console.log("AJAX form submission successful:", response);
                // Re-enable button(s)
                $submitButtons.each(function(i){ $(this).prop('disabled', false).html(originalButtonHtml[i]); });

                if (successCallback) {
                    successCallback(response); // Pass the response data to the callback
                } else {
                    // Default success behavior if no specific callback provided
                    alert("Sikeres művelet!");
                    closeModalInternal(); // Close modal on default success
                    // Optionally refresh parent page data if a standard function exists
                    if (window.parent && typeof window.parent.refreshCourseData === 'function') {
                        window.parent.refreshCourseData();
                    }
                }
            },
            error: function(xhr, status, error) {
                console.error("AJAX form submission error:", { url: url, status: status, error: error, response: xhr.responseText });
                // Re-enable button(s)
                $submitButtons.each(function(i){ $(this).prop('disabled', false).html(originalButtonHtml[i]); });

                if (errorCallback) {
                    // Pass detailed error info to the callback
                    errorCallback(xhr, status, error);
                } else {
                    // Default error display if no callback specified
                    var errorMsg = 'Hiba történt a művelet végrehajtása közben.';
                    try {
                        var responseJson = JSON.parse(xhr.responseText);
                        // Use lowercase 'message' first, then fallback to 'Message'
                        if (responseJson && responseJson.message) errorMsg += "\n" + responseJson.message;
                        else if (responseJson && responseJson.Message) errorMsg += "\n" + responseJson.Message;

                    } catch (e) { /* Ignore if response is not JSON */ }
                    alert(errorMsg);
                }
            }
        });
    }

    // Initialize specific event handlers for elements *within* a given container (e.g., the loaded modal)
    function initializeFormHandlers(container) {
        var $container = $(container);
        if (!$container || $container.length === 0) {
            console.warn("initializeFormHandlers called with invalid container.");
            return;
        }
        var containerDesc = $container.attr('id') || $container.prop('tagName');
        console.log("Initializing handlers within container:", containerDesc);

        // --- Handle AJAX forms (using data-ajax="true") ---
        var $ajaxForms = $container.find('form[data-ajax="true"]');
        console.log("Found " + $ajaxForms.length + " AJAX forms in", containerDesc);

        // Use namespaced events to avoid multiple bindings if called repeatedly
        $ajaxForms.off('submit.coursebooking').on('submit.coursebooking', function(e) {
            e.preventDefault(); // Prevent standard form submission
            var formId = this.id || 'anonymous form';
            console.log("AJAX form submitted:", formId);

            var $form = $(this);
            // Get callback function names from data attributes
            var successCallbackName = $form.data('success-callback');
            var errorCallbackName = $form.data('error-callback');

            var successCallback = null;
            if (successCallbackName && typeof window[successCallbackName] === 'function') {
                successCallback = window[successCallbackName];
                console.log("Using success callback:", successCallbackName);
            } else if (successCallbackName) {
                console.warn("Success callback function not found in window scope:", successCallbackName);
            }

            var errorCallback = null;
            if (errorCallbackName && typeof window[errorCallbackName] === 'function') {
                errorCallback = window[errorCallbackName];
                console.log("Using error callback:", errorCallbackName);
            } else if (errorCallbackName) {
                console.warn("Error callback function not found in window scope:", errorCallbackName);
            }

            // Call the generic AJAX submission function
            submitFormAjax(this, successCallback, errorCallback);
        });

        // --- Handle links that should open in a modal (using data-modal="true") ---
        // Note: This allows modals to open other modals if needed.
        var $modalLinks = $container.find('a[data-modal="true"]');
        console.log("Found " + $modalLinks.length + " modal links in", containerDesc);

        $modalLinks.off('click.coursebooking').on('click.coursebooking', function(e) {
            e.preventDefault();
            var url = $(this).attr('href');
            var title = $(this).data('modal-title') || $(this).attr('title') || 'Részletek';
            var width = $(this).data('modal-width'); // Let openModalUrl use defaults if null
            var height = $(this).data('modal-height');

            console.log("Modal link clicked:", { url: url, title: title });
            // Call the main function to open the URL in a modal
            openModalUrl(url, title, { width: width, height: height });
        });

        // --- Initialize Copy Voucher Button ---
        var $copyButton = $container.find("#copyVoucher");
        if ($copyButton.length > 0) {
            console.log("Initializing copy voucher button in", containerDesc);
            $copyButton.off('click.coursebooking').on('click.coursebooking', function() {
                var voucherText = $container.find("#voucherCode").text() || $container.find("#voucherCode").val();
                if (!voucherText) return;

                navigator.clipboard.writeText(voucherText).then(() => {
                    var $btn = $(this);
                    var originalHtml = $btn.html();
                    $btn.html('<i class="fa fa-check"></i> Másolva');
                    $btn.prop('disabled', true);
                    setTimeout(function () {
                        $btn.html(originalHtml).prop('disabled', false);
                    }, 2000); // Show feedback for 2 seconds
                }).catch(err => {
                    console.error('Failed to copy using navigator.clipboard: ', err);
                    // Fallback for older browsers or insecure contexts
                    try {
                        var tempInput = $("<input>");
                        $("body").append(tempInput);
                        tempInput.val(voucherText).select();
                        document.execCommand("copy");
                        tempInput.remove();
                        // Visual feedback (same as above)
                        var $btn = $(this);
                        var originalHtml = $btn.html();
                        $btn.html('<i class="fa fa-check"></i> Másolva');
                        $btn.prop('disabled', true);
                        setTimeout(function () {
                            $btn.html(originalHtml).prop('disabled', false);
                        }, 2000);
                    } catch (fallbackErr) {
                        console.error('Fallback copy command failed:', fallbackErr);
                        alert('Hiba a vágólapra másolás közben. Jelölje ki és másolja kézzel (Ctrl+C).');
                    }
                });
            });
        }

        // --- Initialize the modal's internal close button (".modal-close") ---
        var $closeButton = $container.find(".modal-close");
        if ($closeButton.length > 0) {
            console.log("Initializing .modal-close button in", containerDesc);
            $closeButton.off('click.coursebooking').on('click.coursebooking', function() {
                console.log("Modal-close button clicked inside modal content.");
                closeModalInternal(); // Use the internal close function
            });
        }
    }

    // --- Public API ---
    return {
        /**
         * Initializes the CourseBookingAjax handler.
         * @param {object} settings - Contains moduleId and tabId.
         * @param {number} settings.moduleId - The DNN Module ID.
         * @param {number} settings.tabId - The DNN Tab ID.
         */
        initialize: initialize,

        /**
         * Opens a URL in a jQuery UI modal dialog.
         * @param {string} url - The URL to load content from.
         * @param {string} title - The title for the modal dialog.
         * @param {object} [options] - Optional settings.
         * @param {number} [options.width=800] - Modal width.
         * @param {number} [options.height=600] - Modal height.
         * @param {boolean} [options.showClose=true] - Show title bar close button.
         * @param {function} [options.onClose] - Callback function when dialog closes.
         */
        openModalUrl: openModalUrl,

        // initializeFormHandlers is primarily for internal use after modal load,
        // but expose it if needed for dynamically added content elsewhere.
        /**
         * Initializes AJAX forms, modal links, and other specific controls within a given container.
         * @param {jQuery|HTMLElement|string} container - The container element (or selector) to scan.
         */
        initializeFormHandlers: initializeFormHandlers,
    };
})();