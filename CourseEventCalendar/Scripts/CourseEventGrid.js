/******************************************************************************
 *
 *   Copyright Csaposkola, Hungary.
 *
 * ---------------------------------------------------------------------------
 *   This is a part of the CourseEventCalendar project
 *
 *****************************************************************************/

class CourseEventDialog {
    constructor(
        title,
        description
    ) {
        this.title = title;
        this.description = description;
        this.options = {};
    }

    option(choice, option) {
        this.options[choice] = option;

        return this;
    }

    render() {
        var that = this;

        var $container = $('<div></div>', {
            'class': 'cec-dialog-container dnnFormPopup'
        });
        $container.append($('<h1>' + this.title + '</h1>'));
        $container.append($('<p>' + this.description + '</p>'));
        $container.click(function (e) {
            e.stopPropagation();
        });

        for (const choice in this.options) {
            var option = this.options[choice];
            var cssClass = "dnn" + (option.type || 'Secondary') + "Action";
            var $button = $('<a href="#" class="' + cssClass + ' right cec-dialog-button">' + option.caption + '</a>');
            $button.click(function () { that.onChoiceClick(choice) });
            $container.append($button);
            $container.append('&nbsp;');
        }

        var $overlay = $('<div></div>', {
            'class': 'cec-dialog-overlay'
        });
        $overlay.append($container);
        $overlay.click(function () {
            that.onOverlayClick();
        });


        return $overlay;
    }

    show(callback) {
        if (this.$dialog) {
            var callbacks = this.$dialog.data('callback')
                || [];
            callbacks.push(callback);
            this.$dialog.data('callback', callbacks);
        }

        this.$dialog = this.render();
        this.$dialog.data('callback', [callback]);
        $('body').append(this.$dialog);

    }

    hide(choice) {
        var that = this;
        var callbacks = this.$dialog.data('callback');
        this.$dialog.hide('slow', function () {
            that.$dialog.remove();
            that.$dialog = null;
        })

        if (callbacks) {
            callbacks.forEach(function (item) {
                item(choice);
            });
        }
    }

    onOverlayClick() {
        this.hide('cancel');
    }

    onChoiceClick(choice) {
        this.hide(choice);
    }
}

/******************************************************************************
 *
 *   Copyright Csaposkola, Hungary.
 *
 * ---------------------------------------------------------------------------
 *   This is a part of the CourseEventCalendar project
 *
 *****************************************************************************/

class CourseEventProxy {

    constructor(moduleID, serviceName) {
        this.serviceName = serviceName;
        var sf = $.ServicesFramework(moduleID);
        this.baseUrl = sf.getServiceRoot(serviceName);
    }

    invoke(method, url, data, callback) {
        $.ajax({
            url: url,
            type: method,
            data: data,
            cache: false,
            success: function (response) {
                callback(true, response);
            }
        })
            .fail(function (xhr) {
                var json = xhr.responseJSON ? xhr.responseJSON : null;
                var jsonError = json && json.error ? json.error : null;

                var message = jsonError
                    || `Request from ${url} failed with status: ${xhr.status}`;

                callback(false, message);
            });
    }

    post(url, data, callback) {
        this.invoke('POST', url, data, callback);
    }

    cancel(eventID, callback) {
        this.post(
            this.baseUrl + 'CourseEvent/Cancel',
            {
                EventID: eventID
            },
            callback
        )
    }

    create(startAt, templateID, callback) {
        this.post(
            this.baseUrl + 'CourseEvent/Create',
            {
                StartAt: startAt,
                TemplateID: templateID
            },
            callback
        )
    }

    addParticipant(eventID, data, callback) {
        this.post(
            this.baseUrl + 'CourseEvent/Add',
            {
                EventID: eventID,
                Name: data.name,
                Role: data.role,
                Certificate: data.certificate
            },
            callback
        );
    }
}

/******************************************************************************
 *
 *   Copyright Csaposkola, Hungary.
 *
 * ---------------------------------------------------------------------------
 *   This is a part of the CourseEventCalendar project
 *
 *****************************************************************************/

class CourseEventGridParticipantRow {

    constructor($row) {
        this.changedCallback = null;
        this.$row = $row;
        this.attach();
        this.refresh();
    }

    attach() {
        this.$role = this.$row.find('select[name="ParticipantRole"]');
        this.$participant = this.$row.find('input[name="ParticipantName"]');
        this.$certificate = this.$row.find('input[name="CertificateNumber"]');

        var that = this;
        this.$role.change(function (e) { that.onTypeChanged(); })
    }

    refresh() {
        var role = this.$role.val();
        this.$participant.prop('disabled', !role);
        this.$certificate.prop('disabled', role != 'instructor');
    }

    setVisiblity(visible) {
        if (visible) {
            this.$row.show();
        } else {
            this.$row.hide();
        }
    }

    clearErrors() {
        this.$row.find('.dnnFormError').remove();
    }

    errorFor($element, message) {
        var $error = $element.next('.dnnFormError');
        if (!$error.length) {
            $error = $('<span></span>', {
                'class': 'dnnFormError'
            });
            $element.after($error);

        }

        $error.html(message);
    }

    getData() {
        return {
            role: this.$role.val(),
            name: this.$participant.val(),
            certificate: this.$certificate.val()
        }
    }

    isEmpty() {
        var data = this.getData();
        return !data.role && !data.name && !data.certificate;
    }

    validate() {
        this.clearErrors();

        if (this.isEmpty())
            return true;

        var result = true;
        var data = this.getData();
        if (!data.role) {
            this.errorFor(this.$role, 'Please select participant role.');
            result = false;
        }

        if (!data.name) {
            this.errorFor(this.$participant, 'Please enter participant name.');
            result = false;
        }

        if (data.role == 'instructor' && !/([a-zA-Z\d]{3})-(\d{5})-(\d{3})-(\d{4})-[sSmMlLcC]/gm.test(data.certificate)) {
            this.errorFor(this.$certificate, 'Please enter certificate (The AAA-00000-000-0000-X formatted code).');
            result = false;
        }

        return result;
    }

    onTypeChanged() {
        this.refresh();

        if (this.changedCallback) {
            this.changedCallback();
        }
    }
}

/******************************************************************************
 *
 *   Copyright Csaposkola, Hungary.
 *
 * ---------------------------------------------------------------------------
 *   This is a part of the CourseEventCalendar project
 *
 *****************************************************************************/

class CourseEventGridForm {

    constructor(selector) {
        this.$grid = $(selector);
        this.attach();
        this.refresh();
    }

    attach() {
        var that = this;

        that.rows = [];
        this.$grid.find('[data-role="cec-participant-row"]')
            .each(function (idx, element) {
                var row = new CourseEventGridParticipantRow($(element));
                row.changedCallback = function () { that.onRowChanged(); };
                that.rows.push(row);
            });
    }

    refresh() {
        var isVisible = true;
        this.rows.forEach(function (row) {
            var hasData = !row.isEmpty();
            row.setVisiblity(isVisible || hasData);
            isVisible = !row.isEmpty();
        });
    }

    getData() {
        return this.rows
            .filter(function (row) { return !row.isEmpty(); })
            .map(function (row) { return row.getData(); });
    }

    getDataChain() {
        var data = this.getData();
        for (var i = 0; i < data.length; i++) {
            data[i].next = i < (data.length - 1)
                ? data[i + 1] : null;
        }

        return data;
    }

    validate() {
        var isValid = true;
        this.rows.forEach(function (row) {
            isValid = isValid && row.validate();
        });

        return isValid;
    }

    submit(proxy, data, callback) {
        var that = this;
        proxy.create(
            data.startAt,
            data.templateID,
            function (success, response) {
                if (success) {
                    that.submitParticipants(proxy, response.EventID, callback);
                } else {
                    callback(success, response);
                }
            });
    }

    submitParticipants(proxy, eventID, callback) {
        var data = this.getDataChain();
        if (data.length == 0) {
            callback(true, []);
            return;
        }

        var item = data[0];
        var participants = [];
        var submitNext = function (success, response) {
            if (!success) {
                callback(false, response);
                return;
            }

            participants.push(response);
            item = item.next;
            if (!item)
                callback(true, participants);
            else
                proxy.addParticipant(eventID, item, submitNext);
        }

        proxy.addParticipant(eventID, item, submitNext);
    }

    onRowChanged() {
        this.refresh();
    }
}