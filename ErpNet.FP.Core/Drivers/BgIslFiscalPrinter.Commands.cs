namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Globalization;
    using System.Text;
	using Microsoft.Extensions.Logging;

    public abstract partial class BgIslFiscalPrinter : BgFiscalPrinter
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        protected const byte
            CommandGetStatus = 0x4a,
            CommandGetDeviceInfo = 0x5a,
            CommandMoneyTransfer = 0x46,
            CommandOpenFiscalReceipt = 0x30,
            CommandCloseFiscalReceipt = 0x38,
            CommandAbortFiscalReceipt = 0x3c,
            CommandFiscalReceiptTotal = 0x35,
            CommandFiscalReceiptComment = 0x36,
            CommandFiscalReceiptSale = 0x31,
            CommandPrintDailyReport = 0x45,
            CommandPrintDetailedFMReportByDate = 0x5e,
            CommandPrintShortFMReportByDate = 0x4f,
            CommandGetDateTime = 0x3e,
            CommandSetDateTime = 0x3d,
            CommandGetReceiptStatus = 0x4c,
            CommandGetLastDocumentNumber = 0x71,
            CommandGetTaxIdentificationNumber = 0x63,
            CommandPrintLastReceiptDuplicate = 0x6D,
            CommandSubtotal = 0x33,
            CommandReadLastReceiptQRCodeData = 0x74,
            CommandToPinpad = 0x37;

        // Error for payment with pinpad when transaction may be successful in pinpad, but unsuccessful in fiscal device
        protected const string DatecsPinpadErrorUnfinishedTransaction = "-111560";
        // Pinpad commands – option ‘13’ - After error (-111560) by "CommandFiscalReceiptTotal" and do "Print pinpad receipt"
        protected const string DatecsFinalizePinpadTransactionAndPrintReceipt = "13\t1\t";
        // Pinpad commands – option ‘15’ - Print receipt for pinpad after successful transaction
        protected const string DatecsXPinpadPrintReceipt = "15\t";
        // Pinpad commands – option ‘5’ - End of day from pinpad
        protected const string DatecsXPinpadEndOfDay = "5\t";
        // Pinpad commands – option ‘6’ - Report from pinpad
        protected const string DatecsXPinpadReportFromPinpad = "6\t";

        public override string GetReversalReasonText(ReversalReason reversalReason)
        {
			_logger.LogInformation(">>> Entering GetReversalReasonText(reversalReason={ReversalReason})", reversalReason);
			_logger.LogDebug("<<< Exiting GetReversalReasonText(reversalReason={ReversalReason})", reversalReason);
            return reversalReason switch
            {
                ReversalReason.OperatorError => "1",
                ReversalReason.Refund => "0",
                ReversalReason.TaxBaseReduction => "2",
                _ => "1",
            };
        }

        public virtual (string, DeviceStatus) GetStatus()
        {
			_logger.LogInformation(">>> Entering GetStatus()");
            _logger.LogDebug("<<< Exiting GetStatus()");
            return Request(CommandGetStatus);
        }

        public virtual (string, DeviceStatus) GetTaxIdentificationNumber()
        {
			_logger.LogInformation(">>> Entering GetTaxIdentificationNumber()");
            _logger.LogDebug("<<< Exiting GetTaxIdentificationNumber()");
            return Request(CommandGetTaxIdentificationNumber);
        }

        public virtual (string, DeviceStatus) GetLastDocumentNumber(string closeReceiptResponse)
        {
			_logger.LogInformation(">>> Entering GetLastDocumentNumber(closeReceiptResponse={CloseReceiptResponse})", closeReceiptResponse);
            _logger.LogDebug("<<< Exiting GetLastDocumentNumber(closeReceiptResponse={CloseReceiptResponse})", closeReceiptResponse);
            return Request(CommandGetLastDocumentNumber);
        }
        public virtual (string, DeviceStatus) SubtotalChangeAmount(Decimal amount)
        {
			_logger.LogInformation(">>> Entering SubtotalChangeAmount(amount={Amount})", amount);
			_logger.LogDebug("<<< Exiting SubtotalChangeAmount(amount={Amount})", amount);
            return Request(CommandSubtotal, $"10;{amount.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        public virtual (decimal?, DeviceStatus) GetReceiptAmount()
        {
			_logger.LogInformation(">>> Entering GetReceiptAmount()");
            decimal? receiptAmount = null;

            var (receiptStatusResponse, deviceStatus) = Request(CommandGetReceiptStatus, "T");
            if (!deviceStatus.Ok)
            {
                deviceStatus.AddInfo($"Error occured while reading last receipt status");
                return (null, deviceStatus);
            }

            var fields = receiptStatusResponse.Split(',');
			_logger.LogDebug("GetReceiptAmount(): fields → {Fields}", fields);
            if (fields.Length < 3)
            {
                deviceStatus.AddInfo($"Error occured while parsing last receipt status");
                deviceStatus.AddError("E409", "Wrong format of receipt status");
                return (null, deviceStatus);
            }

            try
            {
                var amountString = fields[2];
                if (amountString.Length > 0)
                {
                    switch (amountString[0])
                    {
                        case '+':
                            receiptAmount = decimal.Parse(amountString.Substring(1), CultureInfo.InvariantCulture) / 100m;
							_logger.LogDebug("GetReceiptAmount(): receiptAmount → {ReceiptAmount}", receiptAmount);
                            break;
                        case '-':
                            receiptAmount = -decimal.Parse(amountString.Substring(1), CultureInfo.InvariantCulture) / 100m;
                            break;
                        default:
                            if (amountString.Contains("."))
                            {
                                receiptAmount = decimal.Parse(amountString, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                receiptAmount = decimal.Parse(amountString, CultureInfo.InvariantCulture) / 100m;
                            }
                            break;
                    }
                }

            }
            catch (Exception e)
            {
				_logger.LogError("Exception in GetReceiptAmount(): {Arg0}", e.Message);
                deviceStatus = new DeviceStatus();
                deviceStatus.AddInfo($"Error occured while parsing the amount of last receipt status");
				_logger.LogDebug("GetReceiptAmount(): deviceStatus → {DeviceStatus}", deviceStatus);
                deviceStatus.AddError("E409", e.Message);
			_logger.LogDebug("<<< Exiting GetReceiptAmount()");
                return (null, deviceStatus);
            }

            return (receiptAmount, deviceStatus);
        }


        public virtual (string, DeviceStatus) MoneyTransfer(decimal amount)
        {
			_logger.LogInformation(">>> Entering MoneyTransfer(amount={Amount})", amount);
            _logger.LogDebug("<<< Exiting MoneyTransfer(amount={Amount})", amount);
            return Request(CommandMoneyTransfer, amount.ToString("F2", CultureInfo.InvariantCulture));
        }

        public virtual (string, DeviceStatus) SetDeviceDateTime(DateTime dateTime)
        {
			_logger.LogInformation(">>> Entering SetDeviceDateTime(dateTime={DateTime})", dateTime);
            _logger.LogDebug("<<< Exiting SetDeviceDateTime(dateTime={DateTime})", dateTime);
            return Request(CommandSetDateTime, dateTime.ToString("dd-MM-yy HH:mm:ss", CultureInfo.InvariantCulture));
        }

        public virtual (string, DeviceStatus) GetFiscalMemorySerialNumber()
        {
			_logger.LogInformation(">>> Entering GetFiscalMemorySerialNumber()");
            var (rawDeviceInfo, deviceStatus) = GetRawDeviceInfo();
            var fields = rawDeviceInfo.Split(',');
			_logger.LogDebug("GetFiscalMemorySerialNumber(): fields → {Fields}", fields);
            if (fields != null && fields.Length > 0)
            {
                return (fields[^1], deviceStatus);
            }
            else
            {
                deviceStatus.AddInfo($"Error occured while reading device info");
                deviceStatus.AddError("E409", $"Wrong number of fields");
				_logger.LogDebug("<<< Exiting GetFiscalMemorySerialNumber()");
                return (string.Empty, deviceStatus);
            }
        }

        public virtual (System.DateTime?, DeviceStatus) GetDateTime()
        {
			_logger.LogInformation(">>> Entering GetDateTime()");
            var (dateTimeResponse, deviceStatus) = Request(CommandGetDateTime);
            if (!deviceStatus.Ok)
            {
                deviceStatus.AddInfo($"Error occured while reading current date and time");
                return (null, deviceStatus);
            }


            if (DateTime.TryParseExact(dateTimeResponse,
                "dd-MM-yy HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime1))
            {
                return (dateTime1, deviceStatus);
            }
            else if (DateTime.TryParseExact(dateTimeResponse,
                "dd.MM.yy HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime2))
            {
                return (dateTime2, deviceStatus);
            }
            else
            {
                deviceStatus.AddInfo($"Error occured while parsing current date and time");
                deviceStatus.AddError("E409", $"Wrong format of date and time");
				_logger.LogDebug("<<< Exiting GetDateTime()");
                return (null, deviceStatus);
            }
        }

        public virtual (string, DeviceStatus) OpenReceipt(
            string uniqueSaleNumber,
            string operatorId,
            string operatorPassword)
        {
			_logger.LogInformation(">>> Entering OpenReceipt()");
            var header = string.Join(",",
                new string[] {
                    String.IsNullOrEmpty(operatorId) ?
                        Options.ValueOrDefault("Operator.ID", "1")
                        :
                        operatorId,
                    String.IsNullOrEmpty(operatorId) ?
                        Options.ValueOrDefault("Operator.Password", "0000").WithMaxLength(Info.OperatorPasswordMaxLength)
                        :
                        operatorPassword,
                    uniqueSaleNumber
                });
				_logger.LogDebug("<<< Exiting OpenReceipt()");
            return Request(CommandOpenFiscalReceipt, header);
        }

        public virtual (string, DeviceStatus) OpenReversalReceipt(
            ReversalReason reason,
            string receiptNumber,
            System.DateTime receiptDateTime,
            string fiscalMemorySerialNumber,
            string uniqueSaleNumber,
            string operatorId,
            string operatorPassword)
        {
			_logger.LogInformation(">>> Entering OpenReversalReceipt()");
            // Protocol: {ClerkNum},{Password},{UnicSaleNum}[{Tab}{Refund}{Reason},{DocLink},{DocLinkDT}{Tab}{FiskMem}
            var headerData = new StringBuilder()
                .Append(
                    String.IsNullOrEmpty(operatorId) ?
                        Options.ValueOrDefault("Administrator.ID", "20")
                        :
                        operatorId
                )
                .Append(',')
                .Append(
                    String.IsNullOrEmpty(operatorPassword) ?
                        Options.ValueOrDefault("Administrator.Password", "9999").WithMaxLength(Info.OperatorPasswordMaxLength)
                        :
                        operatorPassword
                )
                .Append(',')
                .Append(uniqueSaleNumber)
                .Append('\t')
                .Append('R')
                .Append(GetReversalReasonText(reason))
                .Append(',')
                .Append(receiptNumber)
                .Append(',')
                .Append(receiptDateTime.ToString("dd-MM-yy HH:mm:ss", CultureInfo.InvariantCulture))
                .Append('\t')
                .Append(fiscalMemorySerialNumber);

			_logger.LogDebug("<<< Exiting OpenReversalReceipt()");
            return Request(CommandOpenFiscalReceipt, headerData.ToString());
        }

        public virtual (string, DeviceStatus) AddItem(
            int department,
            string itemText,
            decimal unitPrice,
            TaxGroup taxGroup,
            decimal quantity = 0,
            decimal priceModifierValue = 0,
            PriceModifierType priceModifierType = PriceModifierType.None,
            int ItemCode = 999)
        {
			_logger.LogInformation(">>> Entering AddItem()");
            var itemData = new StringBuilder();
            if (department <= 0) {
				_logger.LogDebug("AddItem(): itemData → {ItemData}", itemData);
                itemData
                    .Append(itemText.WithMaxLength(Info.ItemTextMaxLength))
                    .Append('\t').Append(GetTaxGroupText(taxGroup))
                    .Append(unitPrice.ToString("F2", CultureInfo.InvariantCulture));
            }
            else
            {
                itemData
                    .Append(itemText.WithMaxLength(Info.ItemTextMaxLength))
                    .Append('\t').Append(department).Append('\t')
                    .Append(unitPrice.ToString("F2", CultureInfo.InvariantCulture));
            }
            if (quantity != 0)
            {
                itemData
                    .Append('*')
                    .Append(quantity.ToString(CultureInfo.InvariantCulture));
            }
            if (priceModifierType != PriceModifierType.None)
            {
                itemData
                    .Append(
                        priceModifierType == PriceModifierType.DiscountPercent
                        ||
                        priceModifierType == PriceModifierType.SurchargePercent
                        ? ',' : '$')
                    .Append((
                        priceModifierType == PriceModifierType.DiscountPercent
                        ||
                        priceModifierType == PriceModifierType.DiscountAmount
                        ? -priceModifierValue : priceModifierValue).ToString("F2", CultureInfo.InvariantCulture));
            }
			_logger.LogDebug("<<< Exiting AddItem()");
            return Request(CommandFiscalReceiptSale, itemData.ToString());
        }

        public virtual (string, DeviceStatus) AddComment(string text)
        {
			_logger.LogInformation(">>> Entering AddComment(text={Text})", text);
            _logger.LogDebug("<<< Exiting AddComment(text={Text})", text);
            return Request(
                CommandFiscalReceiptComment,
                text.WithMaxLength(Info.CommentTextMaxLength)
            );
        }

        public virtual (string, DeviceStatus) CloseReceipt()
        {
			_logger.LogInformation(">>> Entering CloseReceipt()");
            _logger.LogDebug("<<< Exiting CloseReceipt()");
            return Request(CommandCloseFiscalReceipt);
        }

        public virtual (string, DeviceStatus) AbortReceipt()
        {
			_logger.LogInformation(">>> Entering AbortReceipt()");
            _logger.LogDebug("<<< Exiting AbortReceipt()");
            return Request(CommandAbortFiscalReceipt);
        }

        public virtual (string, DeviceStatus) FullPayment()
        {
			_logger.LogInformation(">>> Entering FullPayment()");
            _logger.LogDebug("<<< Exiting FullPayment()");
            return Request(CommandFiscalReceiptTotal, "\t");
        }

        public virtual (string, DeviceStatus) AddPayment(decimal amount, PaymentType paymentType)
        {
			_logger.LogInformation(">>> Entering AddPayment(amount={Amount},paymentType={PaymentType})", amount, paymentType);
            var paymentData = new StringBuilder()
                .Append('\t')
                .Append(GetPaymentTypeText(paymentType))
                .Append(amount.ToString("F2", CultureInfo.InvariantCulture));
			_logger.LogDebug("<<< Exiting AddPayment(amount={Amount},paymentType={PaymentType})", amount, paymentType);
            return Request(CommandFiscalReceiptTotal, paymentData.ToString());
        }

        public virtual (string, DeviceStatus) PrintDailyReport(bool zeroing = true)
        {
			_logger.LogInformation(">>> Entering PrintDailyReport(zeroing={Zeroing})", zeroing);
            if (zeroing)
            {
                return Request(CommandPrintDailyReport);
            }
            else
            {
				_logger.LogDebug("<<< Exiting PrintDailyReport(zeroing={Zeroing})", zeroing);
                return Request(CommandPrintDailyReport, "2");
            }
        }

        // Protocol: {StartDate},{EndDate}
        // Both dates are 6 bytes, DDMMYY. Omitting the end date makes the device print a
        // monthly (MMYY) or annual (YY) report instead, so both are always sent.
        public virtual (string, DeviceStatus) PrintPeriodReport(
            PeriodReportType type,
            DateTime startDate,
            DateTime endDate)
        {
			_logger.LogInformation(">>> Entering PrintPeriodReport()");
            var command = type == PeriodReportType.Detailed
                ? CommandPrintDetailedFMReportByDate
                : CommandPrintShortFMReportByDate;
            
            var periodData = new StringBuilder()
                .Append(FormatPeriodReportDate(startDate))
                .Append(',')
                .Append(FormatPeriodReportDate(endDate));

			_logger.LogDebug("<<< Exiting PrintPeriodReport()");
            return Request(command, periodData.ToString());
        }

        protected virtual string FormatPeriodReportDate(DateTime date)
        {
			_logger.LogInformation(">>> Entering FormatPeriodReportDate(date={Date})", date);
            _logger.LogDebug("<<< Exiting FormatPeriodReportDate(date={Date})", date);
            return date.ToString("ddMMyy", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Commands that keep the device busy far longer than a regular one, because it prints
        /// the whole requested period before answering. See <see cref="MaxReadRetriesLongRunning"/>.
        /// </summary>
        protected virtual bool IsLongRunningCommand(byte command)
        {
			_logger.LogInformation(">>> Entering IsLongRunningCommand(command={Command})", command);
            _logger.LogDebug("<<< Exiting IsLongRunningCommand(command={Command})", command);
            return command == CommandPrintDetailedFMReportByDate
                || command == CommandPrintShortFMReportByDate;
        }

        public virtual (string, DeviceStatus) GetLastReceiptQRCodeData()
        {
			_logger.LogInformation(">>> Entering GetLastReceiptQRCodeData()");
            _logger.LogDebug("<<< Exiting GetLastReceiptQRCodeData()");
            return Request(CommandReadLastReceiptQRCodeData);
        }

        public virtual (string, DeviceStatus) GetRawDeviceInfo()
        {
			_logger.LogInformation(">>> Entering GetRawDeviceInfo()");
            _logger.LogDebug("<<< Exiting GetRawDeviceInfo()");
            return Request(CommandGetDeviceInfo, "1");
        }
    }
}
