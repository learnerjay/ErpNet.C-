const logger = console;
var availablePrinters = {}
var availablePaymentTypes = ['cash', 'check', 'coupons', 'ext-coupons', 'packaging', 'internal-usage', 'damage', 'card', 'bank', 'reserved1', 'reserved2'];
var availablePrinterConstantsNames = ['itemTextMaxLength', 'commentTextMaxLength'];
// use positional matching of availablePrinterOptions, hintsForPrinterOptions, useOfPrinterOptions and displayTextsForPrinterOptions
var availablePrinterOptions = ['supportPaymentTerminal'];
var hintsForPrinterOptions = ['For fiscal notes with CARD payment, activate the payment terminal attached to the fiscal printer to receive the payment.'];
var useOfPrinterOptions = ['usePaymentTerminal'];
var displayTextsForPrinterOptions = ['Automatically Activate Payment Terminal'];
var printerProperties = {}

function showAvailablePrinters() {
	logger.debug('>>> Entering showAvailablePrinters()');
    $.ajax({
        type: 'GET',
        url: '/printers',
        data: {},
        dataType: 'json',
        timeout: 0,
        context: $('#PrintersList'),
        success: function (data) {
			logger.debug(`>>> Entering success(data=${data})`);
            availablePrinters = data
            if (!printerProperties)
                getPrinterProperties()
            this.html("")
            for (var printerId in data) {
                var printer = data[printerId]
                var url = window.location.protocol +
                    "//" +
                    window.location.host +
                    "/printers/" +
                    printerId
                var characteristics =
                    '<li><strong><a target="blank_" href="' + url + '">' + url + '</a></strong></li>'
                for (var characteristic in printer) {
                    characteristics += '<li>' + characteristic + ':&nbsp;<strong>' + printer[characteristic] + '</strong></li>'
                }

                var printerProps = printerProperties[printer.serialNumber]

                var printerConstantsContent =
                    '<div class="section dark"><h5 style="padding: 0 em; margin: 0;">Printer constants override</h5></div>' +
                    '<div class="section input-group horizontal">';
                for (var pc in availablePrinterConstantsNames) {
                    var printerConstantName = availablePrinterConstantsNames[pc]
                    var printerConstantValue = ''
					logger.debug(`success(data=${data}): printerConstantValue → ${printerConstantValue}`);
                    if (printerProps) {
                        var pv = printerProps.printerConstants[printerConstantName]
                        if (pv) {
                            printerConstantValue = pv
                        }
                    }
                    printerConstantsContent +=
                        '<label style="margin-right: 0;">' + printerConstantName + ':</label>' +
                        '<input id="' + printer.serialNumber + '_pc_' + printerConstantName + '"' +
                        ' title="Enter here the Printer Constant Value for \'' +
                        printerConstantName + '\' payment type" style="margin-left: 0; padding: 0.2em; height: 1.5em; width: 5em;" value="' +
                        printerConstantValue + '" onFocus="this.select()"/>&nbsp;';
                }
                printerConstantsContent += '</div>';

                var paymentMappingsContent =
                    '<div class="section dark"><h5 style="padding: 0 em; margin: 0;">Payment Type to the Printer Protocol\'s Raw Symbols mappings</h5></div>' + 
                    '<div class="section input-group horizontal">';
                var printerProps = printerProperties[printer.serialNumber]
                for (var pi in availablePaymentTypes) {
                    var paymentType = availablePaymentTypes[pi]
                    var paymentMapping = ''
                    if (printerProps) {
                        var pm = printerProps.paymentTypeMappings[paymentType]
                        if (pm) {
                            paymentMapping = pm
                        }
                    }
                    paymentMappingsContent +=
                        '<label style="margin-right: 0;">' + paymentType + ':</label>' +
                        '<input id="' + printer.serialNumber + '_pt_' + paymentType + '"' +
                        ' title="Enter here the Printer Protocol\'s Raw Symbol for \'' +
                        paymentType + '\' payment type" style="margin-left: 0; padding: 0.2em; height: 1.5em; width: 1.5em;" value="' +
                        paymentMapping + '" maxlength="1" onFocus="this.select()"/>&nbsp;';
                }
                paymentMappingsContent += '</div>';

                var printerOpionsContent = '';
				logger.debug(`success(data=${data}): printerOpionsContent → ${printerOpionsContent}`);
                if (printerProps) {
                    for (var i = 0; i < availablePrinterOptions.length; i++) {
                        var po = availablePrinterOptions[i];
                        var optionAvailable = printerProps.printerOptions[po];
                        if (optionAvailable) {
                            var printerOptionName = useOfPrinterOptions[i];
                            var printerOptionHint = hintsForPrinterOptions[i];
                            var printerOptionDisplayText = displayTextsForPrinterOptions[i];
                            var inputId = printer.serialNumber + '_po_' + printerOptionName;
							logger.debug(`success(data=${data}): inputId → ${inputId}`);
                            var printerOptionValue = '';
							logger.debug(`success(data=${data}): printerOptionValue → ${printerOptionValue}`);

                            var pv = printerProps.printerOptions[printerOptionName]
                            //if (pv) {
                                if (pv == 'True')
                                    printerOptionValue = 'checked';
                            //}

                            printerOpionsContent +=
                                '<label title ="' + printerOptionHint + '" for="' + inputId + '" style="margin-right: 0;">' +
                                    printerOptionDisplayText + '</label>' +
                                '<input type = "checkbox"' + ' id="' + inputId + '" title="' + printerOptionHint + '" ' +
                                printerOptionValue + ' onFocus="this.select()"/>&nbsp;';
                        }
                    }
                    if (printerOpionsContent.length > 0) {
                        printerOpionsContent =
                            '<div class="section dark"><h5 style="padding: 0 em; margin: 0;">Printer options</h5></div>' +
                            '<div class="section input-group horizontal">' + printerOpionsContent + '</div>';
                    }
                }

                var section =
                    '<input type="radio" id="available-section-' + printerId + '" aria-hidden="true" name="available">' +
                    '<label style="overflow:hidden;display:inline-block;text-overflow: ellipsis;white-space: nowrap;" for="available-section-' + printerId + '" aria-hidden="true"><strong>' + printerId + '</strong></label>' +
                    '<div><ul>' + characteristics + '</ul>' +

                    '<button class="small primary" onclick="printZReport(\'' + printerId + '\')">Z-Report</button>' +
                    '<button class="small primary" onclick="printXReport(\'' + printerId + '\')">X-Report</button>' +
                    '<button class="small primary" onclick="resetPrinter(\'' + printerId + '\')">Reset</button>' +
                    '<button class="small primary" title="Sync the printer time with the current time on the PC" onclick="syncTime(\'' + printerId + '\')">Sync Time</button>' +
                    '<button class="small primary" title="Prints duplicate of the last fiscal receipt" onclick="printDuplicate(\'' + printerId + '\')">Duplicate</button>' +
                    (printer.supportsPeriodReport ?
                        '<div class="section input-group horizontal">' +
                        '<label for="period-start-' + printerId + '">Period report from</label>' +
                        '<input type="date" id="period-start-' + printerId + '">' +
                        '<label for="period-end-' + printerId + '">to</label>' +
                        '<input type="date" id="period-end-' + printerId + '">' +
                        '<select id="period-type-' + printerId + '">' +
                        '<option value="short">Short</option>' +
                        '<option value="detailed">Detailed</option>' +
                        '</select>' +
                        '<button class="small primary" title="Prints a fiscal memory report for a custom period" onclick="printPeriodReport(\'' + printerId + '\')">Period Report</button>' +
                        '</div>' : '') +
                    '<br /><h4>Advanced properties for printer with serial number ' + printer.serialNumber + '... &#8964;</h4>' +
                    '<div class="card fluid">' +
                    printerConstantsContent +
                    paymentMappingsContent +
                    '<div class="section"><i>If you leave the Value empty, then the default value will be used. Fill only the values, that you want to override.</i></div>' +
                    printerOpionsContent +
                    '<div class="section"><button class="small primary" onclick="applyChanges(\'' + printer.serialNumber + '\')">Apply changes</button></div>' +
                    '</div>'+
                    '</div>'
                this.append(section)
            }
            var printersCount = Object.keys(data).length
			logger.debug(`success(data=${data}): printersCount → ${printersCount}`);
            this.append('<p>Available ' + printersCount + ' printer(s).</p>')
			logger.debug(`<<< Exiting success(data=${data})`);
        },
        error: function(xhr, type) {
			logger.debug(`>>> Entering error(xhr=${xhr},type=${type})`);
            // wait more time
			logger.debug('<<< Exiting showAvailablePrinters()');
            setTimeout(function() { showAvailablePrinters() }, 3000);
			logger.debug(`<<< Exiting error(xhr=${xhr},type=${type})`);
        }
    })
}

function getPrinterProperties(serialNumber) {
	logger.debug(`>>> Entering getPrinterProperties(serialNumber=${serialNumber})`);
    for (var pi in printerProperties) {
        var properties = printerProperties[pi]
        if (properties.printerSerialNumber == serialNumber) {
            return properties
        }
    }
	logger.debug(`<<< Exiting getPrinterProperties(serialNumber=${serialNumber})`);
	logger.debug('>>> Entering getPrinterProperties()');
	logger.debug('<<< Exiting getPrinterProperties()');
    return {}
}

function applyChanges(serialNumber) {
	logger.debug(`>>> Entering applyChanges(serialNumber=${serialNumber})`);
    //printerProperties[serialNumber] = {
    //    paymentTypeMappings: {},
    //    printerConstants: {},
    //    printerOptions: {}
    //}
    for (var pti in availablePaymentTypes) {
        var paymentType = availablePaymentTypes[pti]
        var input = $('#' + serialNumber + '_pt_' + paymentType);
        var v = input.val()
        if (v) {
            printerProperties[serialNumber].paymentTypeMappings[paymentType] = v
        }
    }
    for (var pci in availablePrinterConstantsNames) {
        var printerConstantName = availablePrinterConstantsNames[pci]
        var input = $('#' + serialNumber + '_pc_' + printerConstantName);
        var v = input.val()
        if (v) {
            printerProperties[serialNumber].printerConstants[printerConstantName] = v
        }
    }
    for (var poi in useOfPrinterOptions) {
        var printerOptionName = useOfPrinterOptions[poi];
        var input = document.getElementById(serialNumber + '_po_' + printerOptionName);
		logger.debug(`applyChanges(serialNumber=${serialNumber}): input → ${input}`);
        var v = input.checked;
        if (v) {
            printerProperties[serialNumber].printerOptions[printerOptionName] = "True";
        }
        else {
            printerProperties[serialNumber].printerOptions[printerOptionName] = "False";
			logger.debug(`<<< Exiting applyChanges(serialNumber=${serialNumber})`);
        }
    }
    console.log("props", printerProperties)
    $.ajax({
        type: 'POST',
        url: '/service/printersprops',
        data: JSON.stringify(printerProperties),
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            printerProperties = data
            detectAvailablePrinters()
        },
        error: function (xhr, type) {
            showToastMessage("Cannot apply changes to the printers properties.")
			logger.debug('>>> Entering detectAvailablePrinters()');
			logger.debug('<<< Exiting detectAvailablePrinters()');
        }
    })
}

function autoDetectChanged() {
	logger.debug('>>> Entering autoDetectChanged()');
    $.ajax({
        type: 'GET',
        url: '/service/toggleautodetect',
        data: {},
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            $("#Version").html('ver.' + data.version)
            $("#ServerId").html('Server Id: ' + data.serverId)
            $("#AutoDetect").attr('checked', data.autoDetect ? 'checked' : null)
            showToastMessage("Auto detect is " + (data.autoDetect ? 'turned ON' : 'turned OFF'))
        },
        error: function (xhr, type) {
            showToastMessage("Cannot change auto detect mode.")
        }
    })
	logger.debug('<<< Exiting autoDetectChanged()');
}

function excludePortsChanged() {
	logger.debug('>>> Entering excludePortsChanged()');
    var excludePortListValue = document.getElementById("ExcludePortList").value;
    $.ajax({
        type: 'POST',
        url: '/service/excludeports',
        data: JSON.stringify({
            "ExcludePortList": excludePortListValue
        }),
        contentType: 'application/json',
        timeout: 0,
        success: function (data) {
            document.getElementById("ExcludePortList").value = data.excludePortList;
            showToastMessage("Exclude ports list updated successfully.");
        },
        error: function (xhr, type) {
            showToastMessage("Cannot update exclude ports list.")
        }
    })
	logger.debug('<<< Exiting excludePortsChanged()');
}

function detectionTimeoutChanged() {   
	logger.debug('>>> Entering detectionTimeoutChanged()');
    var detectionTimeoutValue = document.getElementById("DetectionTimeout").value;
    $.ajax({
        type: 'POST',
        url: '/service/detectiontimeout',
        data: JSON.stringify({
            "DetectionTimeout": detectionTimeoutValue
        }),
        contentType: 'application/json',    
        timeout: 0,
        success: function (data) {
            document.getElementById("DetectionTimeout").value = data.detectionTimeout;
            showToastMessage("Detection timeout updated successfully.");
        },
        error: function (xhr, type) {
            showToastMessage("Cannot update detection timeout.")
			logger.debug('>>> Entering error()');
			logger.debug('<<< Exiting error()');
        }
    })
	logger.debug('<<< Exiting detectionTimeoutChanged()');
}

function saveWebAccess() {
	logger.debug('>>> Entering saveWebAccess()');
    var origins = $("#AllowedOrigins").val().split(',').map(s => s.trim()).filter(s => s.length > 0);
    var enablePrivate = $("#EnablePrivateNetwork").is(':checked');

    $.ajax({
        type: 'POST',
        url: '/service/webaccess',
        data: JSON.stringify({
            "allowedOrigins": origins,
            "enablePrivateNetwork": enablePrivate
        }),
        contentType: 'application/json',
        success: function (data) {
            showToastMessage("Web access settings saved. A restart is required for changes to take effect.");
        },
        error: function () {
            showToastMessage("Error saving web access settings.");
        }
    });
	logger.debug('<<< Exiting saveWebAccess()');
}

function showConfiguredPrinters() {
	logger.debug('>>> Entering showConfiguredPrinters()');
    $.ajax({
        type: 'GET',
        url: '/service/printers',
        data: {},
        dataType: 'json',
        timeout: 0,
        context: $('#ConfiguredPrintersList'),
        success: function (data) {
            this.html("")
            for (var printerId in data) {
                var printer = data[printerId]
                var section =
                    '<input type="radio" id="configured-section-' + printerId + '" aria-hidden="true" name="configured">' +
                    '<label style="overflow:hidden;display:inline-block;text-overflow: ellipsis;white-space: nowrap;" for="configured-section-' + printerId + '" aria-hidden="true"><strong>' + printerId + '</strong></label>' +
                    '<div class="input-group vertical">' +
                    '<label for="id-' + printerId + '">Id:</label><input id="id-' + printerId + '" value="' + printerId + '" />' +
                    '<label for="uri-' + printerId + '">Uri:</label><input id="uri-' + printerId + '" value="' + printer.uri + '" />' +
                    '<div class="input-group horizontal">' +
                    '<button class="small primary" onclick="saveSettingsForPrinter(\'' + printerId + '\')">Save settings</button>' +
                    '<button class="small secondary" onclick="deletePrinter(\'' + printerId + '\', \'' + printer.uri + '\')">Delete printer</button>' +
                    '</div></div>'
                this.append(section)
            }
            var printersCount = Object.keys(data).length
            this.append('<p>Configured ' + printersCount + ' printer(s).</p>')
        },
        error: function (xhr, type) {
			logger.debug('<<< Exiting showConfiguredPrinters()');
            // wait more time
            setTimeout(function() { showConfiguredPrinters() }, 3000);
        }
    })
}

function getServerVariables() {
	logger.debug('>>> Entering getServerVariables()');
    $.ajax({
        type: 'GET',
        url: '/service/vars',
        data: {},
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            $("#Version").html('ver.' + data.version)
            $("#ServerId").html('Server Id: ' + data.serverId)
            $("#AutoDetect").attr('checked', data.autoDetect ? 'checked' : null)
            $("#ExcludePortList").attr('value', data.excludePortList) 
            $("#DetectionTimeout").attr('value', data.detectionTimeout)
            if (data.webAccess) {
                $("#AllowedOrigins").val(data.webAccess.allowedOrigins.join(", "));
                $("#EnablePrivateNetwork").prop('checked', data.webAccess.enablePrivateNetwork);
                if (typeof originalOrigins !== 'undefined') {
                    originalOrigins = data.webAccess.allowedOrigins.join(", ");
                }
            }
        },
        error: function (xhr, type) {
            showToastMessage("Cannot get server variables.")
        }
    })
	logger.debug('<<< Exiting getServerVariables()');
}

function getPrinterProperties() {
    $.ajax({
        type: 'GET',
        url: '/service/printersprops',
        data: {},
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            printerProperties = data
            showPrinters()
        },
        error: function (xhr, type) {
            showToastMessage("Cannot get printers properties.")
        }
    })
}

function detectAvailablePrinters() {
	logger.debug('>>> Entering detectAvailablePrinters()');
    availablePrinters = {}
    $("#DetectButton").attr("disabled", "disabled")
    $('#PrintersList').html('<div class="spinner primary"></div>Detecting...')
    $('#ConfiguredPrintersList').html('<div class="spinner primary"></div>Loading...')
    $.ajax({
        type: 'GET',
        url: '/service/detect',
        data: {},
        timeout: 0,
        success: function (data) {
            showPrinters()
            $("#DetectButton").attr("disabled", null)
        },
        error: function (xhr, type) {
            showToastMessage("Cannot detect printers now. Try again later.")
            showPrinters()
            $("#DetectButton").attr("disabled", null)
        }
    })
}

function showPrinters() {
	logger.debug('>>> Entering showPrinters()');
    showAvailablePrinters()
    showConfiguredPrinters()
	logger.debug('<<< Exiting showPrinters()');
}

function configurePrinter() {
	logger.debug('>>> Entering configurePrinter()');
    $("#NewPrinterModal").prop('checked', false)
    $.ajax({
        type: 'POST',
        url: '/service/printers/configure',
        data: JSON.stringify({
            "id": $("#NewPrinterId").val(),
            "uri": $("#NewPrinterUri").val()
        }),
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            detectAvailablePrinters()
        },
        error: function (xhr, type) {
            showToastMessage("Cannot configure the new printer.")
        }
    })
	logger.debug('<<< Exiting configurePrinter()');
}

function deletePrinter(printerId, printerUri) {
	logger.debug(`>>> Entering deletePrinter(printerId=${printerId},printerUri=${printerUri})`);
    $.ajax({
        type: 'POST',
        url: '/service/printers/delete',
        data: JSON.stringify({
            "id": printerId,
            "uri": printerUri
        }),
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            detectAvailablePrinters()
        },
        error: function (xhr, type) {
            showToastMessage("Cannot delete the printer configuration.")
        }
    })
	logger.debug(`<<< Exiting deletePrinter(printerId=${printerId},printerUri=${printerUri})`);
}

function saveSettingsForPrinter(printerId) {
	logger.debug(`>>> Entering saveSettingsForPrinter(printerId=${printerId})`);
    $.ajax({
        type: 'POST',
        url: '/service/printers/delete',
        data: JSON.stringify({
            "id": printerId
        }),
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            $.ajax({
                type: 'POST',
                url: '/service/printers/configure',
                data: JSON.stringify({
                    "id": $("#id-" + printerId).val(),
                    "uri": $("#uri-" + printerId).val(),
                }),
                contentType: 'application/json',
                dataType: 'json',
                timeout: 0,
                success: function (data) {
                    detectAvailablePrinters()
                },
                error: function (xhr, type) {
                    showToastMessage("Cannot save the changes for the printer.")
                }
            })
        },
        error: function (xhr, type) {
            showToastMessage("Cannot delete the old settings.")
        }
    })
	logger.debug(`<<< Exiting saveSettingsForPrinter(printerId=${printerId})`);
}

function syncTime(printerId) {
	logger.debug(`>>> Entering syncTime(printerId=${printerId})`);
    $.ajax({
        type: 'POST',
        url: '/printers/' + printerId + '/datetime',
        data: "{}",
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            if (data.ok) {
                showToastMessage("The printer's time is sync'ed with the current time on the PC.")
            } else {
                var errors = "";
                for (var ix in data.messages) {
                    var message = data.messages[ix]
                    if (message.type == "error") {
                        errors += message.text + "; "
                    }
                }
                showToastMessage("Cannot sync printer's time: " + errors.trim())
            }
        },
        error: function (xhr, type) {
            showToastMessage("Cannot sync printer's time.")
        }
    })
	logger.debug(`<<< Exiting syncTime(printerId=${printerId})`);
}

function printDuplicate(printerId) {
	logger.debug(`>>> Entering printDuplicate(printerId=${printerId})`);
    $.ajax({
        type: 'POST',
        url: '/printers/' + printerId + '/duplicate',
        data: "{}",
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            if (data.ok) {
                showToastMessage("Printing the duplicate of the last fiscal receipt printing is done.")
            } else {
                var errors = "";
                for (var ix in data.messages) {
                    var message = data.messages[ix]
                    if (message.type == "error") {
                        errors += message.text + "; "
                    }
                }
                showToastMessage("Cannot print duplicate of the last fiscal receipt: " + errors.trim())
            }
        },
        error: function (xhr, type) {
            showToastMessage("Cannot print duplicate of the last fiscal receipt.")
        }
    })
	logger.debug(`<<< Exiting printDuplicate(printerId=${printerId})`);
}

function printZReport(printerId) {
	logger.debug(`>>> Entering printZReport(printerId=${printerId})`);
    $.ajax({
        type: 'POST',
        url: '/printers/' + printerId + '/zreport',
        data: {"deviceDateTime":""},
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            if (data.ok) {
                showToastMessage("The Z-Report printing is done.")
            } else {
                var errors = "";
                for (var ix in data.messages) {
                    var message = data.messages[ix]
                    if (message.type == "error") {
                        errors += message.text + "; "
                    }
                }
                showToastMessage("Cannot print the Z-Report: " + errors.trim())
            }
        },
        error: function (xhr, type) {
            showToastMessage("Cannot print the Z-Report.")
        }
    })
	logger.debug(`<<< Exiting printZReport(printerId=${printerId})`);
}

function printPeriodReport(printerId) {
	logger.debug(`>>> Entering printPeriodReport(printerId=${printerId})`);
    var startDate = $('#period-start-' + printerId).val()
    var endDate = $('#period-end-' + printerId).val()
    if (!startDate || !endDate) {
	logger.debug(`printPeriodReport(printerId=${printerId}): startDate → ${startDate}`);
	logger.debug(`printPeriodReport(printerId=${printerId}): endDate → ${endDate}`);
        showToastMessage("Please choose both a start and an end date for the period report.")
        return
    }
    $.ajax({
        type: 'POST',
        url: '/printers/' + printerId + '/periodreport',
        data: JSON.stringify({
            "type": $('#period-type-' + printerId).val(),
            "startDate": startDate,
            "endDate": endDate
        }),
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            if (data.taskId) {
                // The report is still printing. It keeps running on the server, so report the
                // task id instead of a result - see the taskinfo endpoint.
                showToastMessage("The period report is still printing. Task id: " + data.taskId)
            } else if (data.ok) {
                showToastMessage("The period report printing is done.")
            } else {
                var errors = "";
                for (var ix in data.messages) {
                    var message = data.messages[ix]
                    if (message.type == "error") {
                        errors += message.text + "; "
                    }
                }
                showToastMessage("Cannot print the period report: " + errors.trim())
            }
        },
        error: function (xhr, type) {
            showToastMessage("Cannot print the period report.")
        }
    })
	logger.debug(`<<< Exiting printPeriodReport(printerId=${printerId})`);
}

function printXReport(printerId) {
	logger.debug(`>>> Entering printXReport(printerId=${printerId})`);
    $.ajax({
        type: 'POST',
        url: '/printers/' + printerId + '/xreport',
        data: {},
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            if (data.ok) {
                showToastMessage("The X-Report printing is done.")
            } else {
                var errors = "";
                for (var ix in data.messages) {
                    var message = data.messages[ix]
                    if (message.type == "error") {
                        errors += message.text + "; "
                    }
                }
                showToastMessage("Cannot print the X-Report: " + errors.trim())
            }
			logger.debug(`success(data=${data}): errors → ${errors}`);
        },
        error: function (xhr, type) {
            showToastMessage("Cannot print the X-Report.")
        }
    })
	logger.debug(`<<< Exiting printXReport(printerId=${printerId})`);
}

function resetPrinter(printerId) {
	logger.debug(`>>> Entering resetPrinter(printerId=${printerId})`);
    $.ajax({
        type: 'POST',
        url: '/printers/' + printerId + '/reset',
        data: {},
        contentType: 'application/json',
        dataType: 'json',
        timeout: 0,
        success: function (data) {
            if (data.ok) {
                showToastMessage("Printer is reset.")
            } else {
                var errors = "";
                for (var ix in data.messages) {
                    var message = data.messages[ix]
                    if (message.type == "error") {
                        errors += message.text + "; "
                    }
                }
                showToastMessage("Cannot reset the printer: " + errors.trim())
            }
        },
        error: function (xhr, type) {
            showToastMessage("Cannot reset the printer.")
        }
    })
	logger.debug(`<<< Exiting resetPrinter(printerId=${printerId})`);
}

function protocolSelected(protocolTemplate) {
	logger.debug(`>>> Entering protocolSelected(protocolTemplate=${protocolTemplate})`);
    var delimiter = "://"
    var oldValue = $("#NewPrinterUri").val()
    if (oldValue) {
	logger.debug(`protocolSelected(protocolTemplate=${protocolTemplate}): delimiter → ${delimiter}`);
	logger.debug(`protocolSelected(protocolTemplate=${protocolTemplate}): oldValue → ${oldValue}`);
        var oldParts = oldValue.split(delimiter, 2)
        if (oldParts.length == 2) {
		logger.debug(`protocolSelected(protocolTemplate=${protocolTemplate}): oldParts → ${oldParts}`);
            newParts = protocolTemplate.split(delimiter, 2)
            $("#NewPrinterUri").val(newParts[0] + delimiter + oldParts[1])
        }
    } else {
        $("#NewPrinterUri").val(protocolTemplate)
    }
		logger.debug(`<<< Exiting protocolSelected(protocolTemplate=${protocolTemplate})`);
}

function showToastMessage(msg) {
	logger.debug(`>>> Entering showToastMessage(msg=${msg})`);
    var toastArea = $("#ToastArea")
    toastArea.html('<span class="toast">' + msg + '</span>')
    setTimeout(function () { toastArea.html(""); }, 3000);
	logger.debug(`showToastMessage(msg=${msg}): toastArea → ${toastArea}`);
	logger.debug(`<<< Exiting showToastMessage(msg=${msg})`);
}

$(function () {
	logger.debug('>>> Entering function()');
    getServerVariables()
    getPrinterProperties()    
})
