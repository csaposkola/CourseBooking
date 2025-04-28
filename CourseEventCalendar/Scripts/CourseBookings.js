/******************************************************************************
 *
 *   Copyright Csaposkola, Hungary.
 *
 * ---------------------------------------------------------------------------
 *   This is a part of the CourseEventCalendar project
 *
 *****************************************************************************/

class CourseBookingManager {
    constructor(moduleID) {
        this.moduleID = moduleID;
        var sf = $.ServicesFramework(moduleID);
        this.serviceUrl = sf.getServiceRoot('CourseEventCalendar');
        this.antiForgeryToken = sf.getAntiForgeryValue();
        this.headers = {
            "ModuleId": moduleID,
            "TabId": sf.getTabId(),
            "RequestVerificationToken": this.antiForgeryToken
        };
    }

    createBooking(eventId, notes, callback) {
        $.ajax({
            type: "POST",
            url: this.serviceUrl + "Booking/Create",
            beforeSend: this.setHeaders.bind(this),
            data: {
                EventID: eventId,
                Notes: notes || ''
            },
            success: function (response) {
                if (callback) callback(true, response);
            },
            error: function (xhr, status, error) {
                console.error("Error creating booking:", xhr.responseJSON);
                var errorMessage = xhr.responseJSON && xhr.responseJSON.error
                    ? xhr.responseJSON.error
                    : "An error occurred while creating your booking.";
                if (callback) callback(false, errorMessage);
            }
        });
    }

    cancelBooking(bookingId, callback) {
        $.ajax({
            type: "POST",
            url: this.serviceUrl + "Booking/Cancel",
            beforeSend: this.setHeaders.bind(this),
            data: {
                BookingID: bookingId
            },
            success: function (response) {
                if (callback) callback(true, response);
            },
            error: function (xhr, status, error) {
                console.error("Error cancelling booking:", xhr.responseJSON);
                var errorMessage = xhr.responseJSON && xhr.responseJSON.error
                    ? xhr.responseJSON.error
                    : "An error occurred while cancelling your booking.";
                if (callback) callback(false, errorMessage);
            }
        });
    }

    getUserBookings(callback) {
        $.ajax({
            type: "GET",
            url: this.serviceUrl + "Booking/MyBookings",
            beforeSend: this.setHeaders.bind(this),
            success: function (response) {
                if (callback) callback(true, response);
            },
            error: function (xhr, status, error) {
                console.error("Error getting bookings:", xhr.responseJSON);
                var errorMessage = xhr.responseJSON && xhr.responseJSON.error
                    ? xhr.responseJSON.error
                    : "An error occurred while retrieving your bookings.";
                if (callback) callback(false, errorMessage);
            }
        });
    }

    getEventBookings(eventId, callback) {
        $.ajax({
            type: "GET",
            url: this.serviceUrl + "Booking/EventBookings?eventId=" + eventId,
            beforeSend: this.setHeaders.bind(this),
            success: function (response) {
                if (callback) callback(true, response);
            },
            error: function (xhr, status, error) {
                console.error("Error getting event bookings:", xhr.responseJSON);
                var errorMessage = xhr.responseJSON && xhr.responseJSON.error
                    ? xhr.responseJSON.error
                    : "An error occurred while retrieving event bookings.";
                if (callback) callback(false, errorMessage);
            }
        });
    }

    setHeaders(xhr) {
        xhr.setRequestHeader("ModuleId", this.headers.ModuleId);
        xhr.setRequestHeader("TabId", this.headers.TabId);
        xhr.setRequestHeader("RequestVerificationToken", this.headers.RequestVerificationToken);
    }
}