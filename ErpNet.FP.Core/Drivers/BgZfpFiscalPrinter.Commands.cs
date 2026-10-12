namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Globalization;
    using System.Text;
	using Microsoft.Extensions.Logging;

    public partial class BgZfpFiscalPrinter : BgFiscalPrinter
    {
        protected const byte
            CommandReadFDNumbers = 0x60,
            CommandGetStatus = 0x20,
            CommandVersion = 0x21,
            CommandPrintDailyFiscalReport = 0x7c,
            CommandPrintDetailedFMReportByDate = 0x7a,
            CommandPrintBriefFMReportByDate = 0x7b,
            CommandNoFiscalRAorPOAmount = 0x3b,
            CommandOpenReceipt = 0x30,
            CommandCloseReceipt = 0x38,
            CommandFullPaymentAndCloseReceipt = 0x36,
            CommandAbortReceipt = 0x39,
            CommandSellCorrection = 0x31,
            CommandSellCorrectionDepartment = 0x34,
            CommandFreeText = 0x37,
            CommandPayment = 0x35,
            CommandGetDateTime = 0x68,
            CommandSetDateTime = 0x48,
            CommandSubtotal = 0x33,
            CommandReadLastReceiptQRCodeData = 0x72,
            CommandGetTaxIdentificationNumber = 0x61,
            CommandReadDailyAvailableAmounts = 0x6E,
            CommandPrintLastReceiptDuplicate = 0x3A,
            CommandGSCommand = 0x1d;
        protected const byte
            // Protocol: 36 symbols for article's name. 34 symbols are printed on paper.
            // Attention: ItemText should be padded right with spaces until reaches mandatory 
            // length of 36 symbols. Otherwise we will have syntax error!
            ItemTextMandatoryLength = 36;

        public virtual (string, DeviceStatus) GetStatus()
        {
			_logger.LogInformation(">>> Entering GetStatus()");
            var (deviceStatus, _ /* ignore commandStatus */) = ParseResponseAsByteArray(RawRequest(CommandGetStatus, null));
			_logger.LogDebug("<<< Exiting GetStatus()");
            return ("", ParseStatus(deviceStatus));
        }

        public virtual (string, DeviceStatus) GetTaxIdentificationNumber()
        {
			_logger.LogInformation(">>> Entering GetTaxIdentificationNumber()");
            var (response, deviceStatus) = Request(CommandGetTaxIdentificationNumber);
            var commaFields = response.Split(';');
			_logger.LogDebug("GetTaxIdentificationNumber(): commaFields → {CommaFields}", commaFields);
            if (commaFields.Length > 1)
            {
                return (commaFields[0].Trim(), deviceStatus);
            }
			_logger.LogDebug("<<< Exiting GetTaxIdentificationNumber()");
            return (string.Empty, deviceStatus);
        }

        public virtual (string, DeviceStatus) SubtotalChangeAmount(Decimal amount)
        {
			_logger.LogInformation(">>> Entering SubtotalChangeAmount(amount={Amount})", amount);
            // <OptionPrinting[1]> <;> <OptionDisplay[1]> {<':'> <DiscAddV[1..8]>} {<','>< DiscAddP[1..7] >}
			_logger.LogDebug("<<< Exiting SubtotalChangeAmount(amount={Amount})", amount);
            return Request(CommandSubtotal, $"1;0:{amount.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        public virtual (string, DeviceStatus) GetLastReceiptQRCodeData()
        {
			_logger.LogInformation(">>> Entering GetLastReceiptQRCodeData()");
            _logger.LogDebug("<<< Exiting GetLastReceiptQRCodeData()");
            return Request(CommandReadLastReceiptQRCodeData, "B");
        }

        public virtual (string, DeviceStatus) GetFiscalMemorySerialNumber()
        {
			_logger.LogInformation(">>> Entering GetFiscalMemorySerialNumber()");
            var (rawDeviceInfo, deviceStatus) = GetRawDeviceInfo();
            var fields = rawDeviceInfo.Split(";");
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

        public virtual (DateTime?, DeviceStatus) GetDateTime()
        {
			_logger.LogInformation(">>> Entering GetDateTime()");
            var (dateTimeResponse, deviceStatus) = Request(CommandGetDateTime);
            if (!deviceStatus.Ok)
            {
                deviceStatus.AddInfo($"Error occured while reading current date and time");
                return (null, deviceStatus);
            }

            try
            {
                var dateTime = DateTime.ParseExact(dateTimeResponse,
                    "dd-MM-yyyy HH:mm",
                    CultureInfo.InvariantCulture);
                return (dateTime, deviceStatus);
            }
            catch
            {
                deviceStatus.AddInfo($"Error occured while parsing current date and time");
                deviceStatus.AddError("E409", $"Wrong format of date and time");
				_logger.LogDebug("<<< Exiting GetDateTime()");
                return (null, deviceStatus);
            }
        }

        public virtual (string, DeviceStatus) SetDeviceDateTime(DateTime dateTime)
        {
			_logger.LogInformation(">>> Entering SetDeviceDateTime(dateTime={DateTime})", dateTime);
            _logger.LogDebug("<<< Exiting SetDeviceDateTime(dateTime={DateTime})", dateTime);
            return Request(CommandSetDateTime, dateTime.ToString("dd-MM-yy HH:mm:ss", CultureInfo.InvariantCulture));
        }

        public virtual (string, DeviceStatus) MoneyTransfer(TransferAmount transferAmount)
        {
			_logger.LogInformation(">>> Entering MoneyTransfer(transferAmount={TransferAmount})", transferAmount);
			_logger.LogDebug("<<< Exiting MoneyTransfer(transferAmount={TransferAmount})", transferAmount);
            return Request(CommandNoFiscalRAorPOAmount, string.Join(";", new string[]
            {
                String.IsNullOrEmpty(transferAmount.Operator) ?
                    Options.ValueOrDefault("Operator.ID", "1")
                    :
                    transferAmount.Operator,
                String.IsNullOrEmpty(transferAmount.OperatorPassword) ?
                    Options.ValueOrDefault("Operator.Password", "0000")
                    :
                    transferAmount.OperatorPassword,
                "0", // Protocol: Reserved 
                transferAmount.Amount.ToString("F2", CultureInfo.InvariantCulture)
            }));
        }

        public virtual (string, DeviceStatus) OpenReceipt(
            string uniqueSaleNumber,
            string operatorId,
            string operatorPassword)
        {
			_logger.LogInformation(">>> Entering OpenReceipt()");
            // Protocol: <OperNum[1..2]> <;> <OperPass[6]> <;> <ReceiptFormat[1]> <;> 
            //           <PrintVAT[1]> <;> <FiscalRcpPrintType[1]> {<’$’> <UniqueReceiptNumber[24]>}
            return Request(CommandOpenReceipt, string.Join(";", new string[] {
                String.IsNullOrEmpty(operatorId) ?
                    Options.ValueOrDefault("Operator.ID", "1")
                    :
                    operatorId,
                String.IsNullOrEmpty(operatorPassword) ?
                    Options.ValueOrDefault("Operator.Password", "0000")
                    :
                    operatorPassword,
                "1", // Protocol: Detailed
                "1", // Protocol: Include VAT
                "2$"+uniqueSaleNumber, // Protocol: Buffered printing (4) is faster than (2) postponed printing, 
                // but there are problems with read timeout because 
                // the Fiscal Device becomes non-responsable when 
                // there are many rows to be printed.
                // Delimiter '$' before USN.
            }));
			_logger.LogDebug("<<< Exiting OpenReceipt()");
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
            // Protocol: <OperNum[1..2]> <;> <OperPass[6]> <;> <ReceiptFormat[1]> <;>
            //            < PrintVAT[1] > <;> < StornoRcpPrintType[1] > <;> < StornoReason[1] > <;>
            //            < RelatedToRcpNum[1..6] > <;> < RelatedToRcpDateTime ”DD-MM-YY HH:MM[:SS]”> <;>
            //            < FMNum[8] > {<;> < RelatedToURN[24] >}            
            return Request(CommandOpenReceipt, string.Join(";", new string[] {
                String.IsNullOrEmpty(operatorId) ?
                    Options.ValueOrDefault("Operator.ID", "1")
                    :
                    operatorId,
                String.IsNullOrEmpty(operatorPassword) ?
                    Options.ValueOrDefault("Operator.Password", "0000")
                    :
                    operatorPassword,
                "1", // Protocol: Detailed
                "1", // Protocol: Include VAT
                "D", // Protocol: Buffered printing
                GetReversalReasonText(reason),
                receiptNumber,
                receiptDateTime.ToString("dd-MM-yy HH:mm:ss", CultureInfo.InvariantCulture),
                fiscalMemorySerialNumber,
                uniqueSaleNumber
            }));
			_logger.LogDebug("<<< Exiting OpenReversalReceipt()");
        }

        public virtual (string, DeviceStatus) AddItem(
            int department,
            string itemText,
            decimal unitPrice,
            TaxGroup taxGroup,
            decimal quantity = 0,
            decimal priceModifierValue = 0,
            PriceModifierType priceModifierType = PriceModifierType.None)
        {
			_logger.LogInformation(">>> Entering AddItem()");
            var itemData = new FrameBuilder(PrinterEncoding);

			_logger.LogDebug("AddItem(): itemData → {ItemData}", itemData);
            if (department <= 0) 
            {
                // Protocol: <NamePLU[36]><;><OptionVATClass[1]><;><Price[1..10]>{<'*'>< Quantity[1..10]>}
                //           {<','><DiscAddP[1..7]>}{<':'><DiscAddV[1..8]>}
                itemData
                    .Append(itemText.WithMaxLength(Info.ItemTextMaxLength).PadRight(ItemTextMandatoryLength))
                    .Append(';')
                    .Append(GetTaxGroupText(taxGroup))
                    .Append(';')
                    .Append(unitPrice.ToString("F2", CultureInfo.InvariantCulture));
            }
            else
            {
                // Protocol: <NamePLU[36]><;><DepNum[1]><;><Price[1..10]>{<'*'>< Quantity[1..10]>}
                //           {<','><DiscAddP[1..7]>}{<':'><DiscAddV[1..8]>}
                itemData
                    .Append(itemText.WithMaxLength(Info.ItemTextMaxLength).PadRight(ItemTextMandatoryLength))
                    .Append(';')
                    // DepNum is one raw byte = dept + 0x80 (Dep01=0x81 ... Dep19=0x93).
                    // Sent via FrameBuilder.Append(byte) to bypass CP1251 text encoding,
                    // which cannot round-trip U+0080–U+009F through char.
                    .Append((byte)(department + 0x80))
                    .Append(';')
                    .Append(unitPrice.ToString("F2", CultureInfo.InvariantCulture));
            }

            if (quantity != 0)
            {
                itemData
                    .Append('*')
                    .Append(quantity.ToString(CultureInfo.InvariantCulture));
            }

            switch (priceModifierType)
            {
                case PriceModifierType.DiscountPercent:
                    itemData
                        .Append(',')
                        .Append((-priceModifierValue).ToString("F2", CultureInfo.InvariantCulture));
                    break;
                case PriceModifierType.DiscountAmount:
                    itemData
                        .Append(':')
                        .Append((-priceModifierValue).ToString("F2", CultureInfo.InvariantCulture));
                    break;
                case PriceModifierType.SurchargePercent:
                    itemData
                        .Append(',')
                        .Append(priceModifierValue.ToString("F2", CultureInfo.InvariantCulture));
                    break;
                case PriceModifierType.SurchargeAmount:
                    itemData
                        .Append(':')
                        .Append(priceModifierValue.ToString("F2", CultureInfo.InvariantCulture));
                    break;
                default:
                    break;
            }

            if (department <= 0) 
            {
                return Request(CommandSellCorrection, itemData.ToArray());
            }
            else
            {
			_logger.LogDebug("<<< Exiting AddItem()");
                return Request(CommandSellCorrectionDepartment, itemData.ToArray());
            }
        }

        public virtual (string, DeviceStatus) AddComment(string text)
        {
			_logger.LogInformation(">>> Entering AddComment(text={Text})", text);
            _logger.LogDebug("<<< Exiting AddComment(text={Text})", text);
            return Request(CommandFreeText, text.WithMaxLength(Info.CommentTextMaxLength));
        }

        public virtual (string, DeviceStatus) CloseReceipt()
        {
			_logger.LogInformation(">>> Entering CloseReceipt()");
            _logger.LogDebug("<<< Exiting CloseReceipt()");
            return Request(CommandCloseReceipt);
        }

        public virtual (string, DeviceStatus) AbortReceipt()
        {
			_logger.LogInformation(">>> Entering AbortReceipt()");
            _logger.LogDebug("<<< Exiting AbortReceipt()");
            return Request(CommandAbortReceipt);
        }

        public virtual (string, DeviceStatus) FullPaymentAndCloseReceipt()
        {
			_logger.LogInformation(">>> Entering FullPaymentAndCloseReceipt()");
            _logger.LogDebug("<<< Exiting FullPaymentAndCloseReceipt()");
            return Request(CommandFullPaymentAndCloseReceipt);
        }

        public virtual (string, DeviceStatus) AddPayment(
            decimal amount,
            PaymentType paymentType)
        {
			_logger.LogInformation(">>> Entering AddPayment()");
            // Protocol: input: <PaymentType [1..2]> <;> <OptionChange [1]> <;> <Amount[1..10]> {<;><OptionChangeType[1]>}
			_logger.LogDebug("<<< Exiting AddPayment()");
            return Request(CommandPayment, string.Join(";", new string[] {
                GetPaymentTypeText(paymentType),
                "1", // Procotol: Without change
                amount.ToString("F2", CultureInfo.InvariantCulture)+"*"
            }));
        }

        public virtual (string, DeviceStatus) PrintDailyReport(bool zeroing = true)
        {
			_logger.LogInformation(">>> Entering PrintDailyReport(zeroing={Zeroing})", zeroing);
            if (zeroing)
            {
                return Request(CommandPrintDailyFiscalReport, "Z");
            }
            else
            {
				_logger.LogDebug("<<< Exiting PrintDailyReport(zeroing={Zeroing})", zeroing);
                return Request(CommandPrintDailyFiscalReport, "X");
            }
        }

        // Protocol: <StartDate "DDMMYY"> <;> <EndDate "DDMMYY">
        public virtual (string, DeviceStatus) PrintPeriodReport(
            PeriodReportType type,
            DateTime startDate,
            DateTime endDate)
        {
			_logger.LogInformation(">>> Entering PrintPeriodReport()");
            var command = type == PeriodReportType.Detailed
                ? CommandPrintDetailedFMReportByDate
                : CommandPrintBriefFMReportByDate;

					_logger.LogDebug("<<< Exiting PrintPeriodReport()");
            return Request(
                command,
                string.Join(
                    ";",
                    [
                        FormatPeriodReportDate(startDate),
                        FormatPeriodReportDate(endDate)
                    ]));
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
                || command == CommandPrintBriefFMReportByDate;
        }

        public virtual (string, DeviceStatus) GetRawDeviceInfo()
        {
			_logger.LogInformation(">>> Entering GetRawDeviceInfo()");
            var (responseFD, _) = Request(CommandReadFDNumbers);
            var (responseV, deviceStatus) = Request(CommandVersion);
			_logger.LogDebug("<<< Exiting GetRawDeviceInfo()");
            return (responseV + responseFD, deviceStatus);
        }
    }
}
