#nullable enable
namespace ErpNet.FP.Core.Drivers.BgSis
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using ErpNet.FP.Core.Configuration;
    using Newtonsoft.Json.Linq;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fiscal printer implementation for the SIS Fiscal Module (JSON-RPC over HTTP).
    /// </summary>
    /// <seealso cref="ErpNet.FP.Core.Drivers.BgFiscalPrinter" />
    public partial class BgSisJsonFiscalPrinter : BgFiscalPrinter
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        // A "comment" line (emitted as freeprint before a sale, or as an item's textlines after one)
        // accepts digits only in its first 31 characters; a digit further right is rejected by the
        // device with EM_PARA_WRONG_FORMAT (0x5D). Validate it up front so the caller gets a clear
        // message instead of the opaque device error. The rule is field-specific and verified live:
        // it applies ONLY to comments - sale descriptions, footer comments (textAfterPayment) and
        // subtotal-modifier text (subtotalText) are all exempt, so they must NOT be flagged.
        private const int CommentDigitLimit = 31;

        public BgSisJsonFiscalPrinter(
            IChannel channel,
            ServiceOptions serviceOptions,
            IDictionary<string, string>? options = null)
            : base(channel, serviceOptions, options) { }

        /// <summary>
        /// The optional POS number. It is only emitted when explicitly configured via the "PosId"
        /// option; the device accepts printReceipt without it. When present, it must be at most 6
        /// symbols - the MF-P1200DN rejects a longer posId with EM_PARA_WRONG_FORMAT (0x5D).
        /// </summary>
        protected string? GetPosId()
        {
			_logger.LogInformation(">>> Entering GetPosId()");
            // Preferred: per-printer service config (appsettings PrintersProperties.<serial>.PrinterOptions.posId).
            // Fallback: options passed programmatically to the driver.
            var posId = ServiceOptions.GetPrinterOption(Info.SerialNumber, "posId");
            if (string.IsNullOrEmpty(posId) && Options.TryGetValue("PosId", out var value))
            {
                posId = value;
            }

            if (string.IsNullOrEmpty(posId))
            {
                return null;
            }

			_logger.LogDebug("<<< Exiting GetPosId()");
            return posId.Length > 6 ? posId.Substring(0, 6) : posId;
        }

        public override string GetTaxGroupText(TaxGroup taxGroup)
        {
			_logger.LogInformation(">>> Entering GetTaxGroupText(taxGroup={TaxGroup})", taxGroup);
            if (taxGroup == TaxGroup.Unspecified)
            {
                throw new StandardizedStatusMessageException($"Tax group {taxGroup} unsupported", "E411");
            }

            // SIS enumVatCategory is 0-based: A=0, B=1 ... H=7; TaxGroup is 1-based.
			_logger.LogDebug("<<< Exiting GetTaxGroupText(taxGroup={TaxGroup})", taxGroup);
            return ((int)taxGroup - 1).ToString(CultureInfo.InvariantCulture);
        }

        public override IDictionary<PaymentType, string> GetPaymentTypeMappings()
        {
            var paymentTypeMappings = new Dictionary<PaymentType, string>
            {
                { PaymentType.Cash,          "0" },  // Cash
                { PaymentType.Bank,          "1" },  // Bank transfer
                { PaymentType.Card,          "2" },  // Credit/debit card
                { PaymentType.Check,         "3" },  // Cheque
                { PaymentType.InternalUsage, "4" },  // Internal usage
                { PaymentType.Coupons,       "5" },  // Voucher
                { PaymentType.ExtCoupons,    "6" },  // External voucher
                { PaymentType.Reserved1,     "7" },  // NZOK
                { PaymentType.Packaging,     "8" }   // Empties (packaging)
            };

            ServiceOptions.RemapPaymentTypes(Info.SerialNumber, paymentTypeMappings);

            return paymentTypeMappings;
        }

        public override DeviceStatusWithDateTime CheckStatus()
        {
			_logger.LogInformation(">>> Entering CheckStatus()");
            var (json, status) = Request("getStatus");
            var statusEx = new DeviceStatusWithDateTime(status);
            // "dd-MM-yyyy HH:mm:ss"
            var timestamp = json.Value<string>("timestamp");
            if (!string.IsNullOrEmpty(timestamp)
                && DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                statusEx.DeviceDateTime = dt;
            }

			_logger.LogDebug("<<< Exiting CheckStatus()");
            return statusEx;
        }

        public override DeviceStatus SetDateTime(CurrentDateTime currentDateTime)
        {
			_logger.LogInformation(">>> Entering SetDateTime(currentDateTime={CurrentDateTime})", currentDateTime);
            var time = currentDateTime.DeviceDateTime.ToString("HH:mm:ss;dd/MM/yy", CultureInfo.InvariantCulture);
            var (_, status) = Request("setTime", null, new JObject { ["time"] = time });
			_logger.LogDebug("<<< Exiting SetDateTime(currentDateTime={CurrentDateTime})", currentDateTime);
            return status;
        }

        public override DeviceStatusWithCashAmount Cash(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering Cash(credentials=<redacted>)");
            var (json, status) = Request("getCashBalance");
            var statusEx = new DeviceStatusWithCashAmount(status);
            var cashBalance = json.Value<string>("cashBalance");
            if (!string.IsNullOrEmpty(cashBalance)
                && decimal.TryParse(cashBalance, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            {
                statusEx.Amount = amount;
            }

			_logger.LogDebug("<<< Exiting Cash(credentials=<redacted>)");
            return statusEx;
        }

        public override DeviceStatus PrintMoneyDeposit(TransferAmount transferAmount)
        {
			_logger.LogInformation(">>> Entering PrintMoneyDeposit(transferAmount={TransferAmount})", transferAmount);
			_logger.LogDebug("<<< Exiting PrintMoneyDeposit(transferAmount={TransferAmount})", transferAmount);
            return CashHandling(transferAmount.Amount, transferAmount.Operator);
        }

        public override DeviceStatus PrintMoneyWithdraw(TransferAmount transferAmount)
        {
			_logger.LogInformation(">>> Entering PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            if (transferAmount.Amount < 0m)
            {
                throw new StandardizedStatusMessageException("Withdraw amount must be positive number", "E403");
            }

			_logger.LogDebug("<<< Exiting PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            return CashHandling(-transferAmount.Amount, transferAmount.Operator);
        }

        protected DeviceStatus CashHandling(decimal amount, string @operator)
        {
			_logger.LogInformation(">>> Entering CashHandling(amount={Amount})", amount);
            var begin = new JObject
            {
                ["operatorNumber"] = int.TryParse(@operator, out var operatorNumber) ? operatorNumber : 1
            };

            var posId = GetPosId();
            if (!string.IsNullOrEmpty(posId))
            {
                begin["posId"] = posId;
            }

            var prms = new JObject
            {
                ["beginFiscalReceiptInput"] = begin,
                ["amount"] = amount.ToString(CultureInfo.InvariantCulture)
            };

            var (_, status) = Request("cashHandling", prms);

			_logger.LogDebug("<<< Exiting CashHandling(amount={Amount})", amount);
            return status;
        }

        public override (ReceiptInfo, DeviceStatus) PrintReceipt(Receipt receipt)
        {
			_logger.LogInformation(">>> Entering PrintReceipt(receipt={Receipt})", receipt);
			_logger.LogDebug("<<< Exiting PrintReceipt(receipt={Receipt})", receipt);
            return PrintFiscalReceipt(receipt, null);
        }

        public override (ReceiptInfo, DeviceStatus) PrintReversalReceipt(ReversalReceipt reversalReceipt)
        {
			_logger.LogInformation(">>> Entering PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
			_logger.LogDebug("<<< Exiting PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
            return PrintFiscalReceipt(reversalReceipt, reversalReceipt);
        }

        public override DeviceStatus ValidateReceipt(Receipt receipt)
        {
			_logger.LogInformation(">>> Entering ValidateReceipt(receipt={Receipt})", receipt);
            var status = base.ValidateReceipt(receipt);
            if (status.Ok)
            {
                ValidateCommentDigits(status, receipt);
            }

			_logger.LogDebug("<<< Exiting ValidateReceipt(receipt={Receipt})", receipt);
            return status;
        }

        protected static void ValidateCommentDigits(DeviceStatus status, Receipt receipt)
        {
			_logger.LogInformation(">>> Entering ValidateCommentDigits(status={Status},receipt={Receipt})", status, receipt);
            if (receipt.Items == null)
            {
                return;
            }

            var row = 0;
            foreach (var item in receipt.Items)
            {
                row++;
                if (item.Type != ItemType.Comment)
                {
                    continue;
                }

                for (var i = CommentDigitLimit; i < item.Text.Length; i++)
                {
                    if (char.IsDigit(item.Text[i]))
                    {
                        status.AddError("E403",
                            $"Item {row}: a comment line accepts digits only in the first {CommentDigitLimit} characters");
                        
                        break;
                    }
                }
            }
			_logger.LogDebug("<<< Exiting ValidateCommentDigits(status={Status},receipt={Receipt})", status, receipt);
        }

        public override DeviceStatus ValidateInvoice(Invoice invoice)
        {
			_logger.LogInformation(">>> Entering ValidateInvoice(invoice={Invoice})", invoice);
            var status = ValidateInvoiceCore(invoice);
            if (status.Ok)
            {
                ValidateSisRecipient(status, invoice.Recipient);
            }

			_logger.LogDebug("<<< Exiting ValidateInvoice(invoice={Invoice})", invoice);
            return status;
        }

        public override DeviceStatus ValidateCreditNote(CreditNote creditNote)
        {
			_logger.LogInformation(">>> Entering ValidateCreditNote(creditNote={CreditNote})", creditNote);
            var status = ValidateCreditNoteCore(creditNote);

            if (status.Ok)
            {
                ValidateSisRecipient(status, creditNote.Recipient);

                if (string.IsNullOrEmpty(creditNote.FiscalDeviceSerialNumber))
                {
                    status.AddError("E405", "FiscalDeviceSerialNumber of the original invoice is required by this device");
                }
            }

			_logger.LogDebug("<<< Exiting ValidateCreditNote(creditNote={CreditNote})", creditNote);
            return status;
        }

        /// <summary>
        /// SIS-specific recipient checks not covered by the neutral validation: the device requires a
        /// city as a separate field, and it can only encode a subset of identifier types. Reported here
        /// so the caller gets a clear error up front instead of an opaque device error at print time.
        /// </summary>
        protected void ValidateSisRecipient(DeviceStatus status, Recipient? recipient)
        {
			_logger.LogInformation(">>> Entering ValidateSisRecipient(status={Status},recipient={Recipient})", status, recipient);
            if (recipient == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(recipient.City))
            {
                status.AddError("E405", "Recipient \"city\" is required by this device");
            }

            try
            {
                GetIdentifierTypeCode(recipient.IdentifierType);
            }
            catch (StandardizedStatusMessageException e)
            {
                status.AddError(e.Code, e.Message);
            }
			_logger.LogDebug("<<< Exiting ValidateSisRecipient(status={Status},recipient={Recipient})", status, recipient);
        }

        public override (ReceiptInfo, DeviceStatus) PrintInvoice(Invoice invoice)
        {
			_logger.LogInformation(">>> Entering PrintInvoice(invoice={Invoice})", invoice);
            JObject prms;
            try
            {
                prms = BuildReceiptParams(invoice, null);
                prms["invoiceData"] = BuildInvoiceData(invoice);
            }
            catch (StandardizedStatusMessageException e)
            {
			_logger.LogDebug("<<< Exiting PrintInvoice(invoice={Invoice})", invoice);
                return ErrorResult(e);
            }

            return ExecutePrint(prms, invoice, invoice.Number);
        }

        public override (ReceiptInfo, DeviceStatus) PrintCreditNote(CreditNote creditNote)
        {
			_logger.LogInformation(">>> Entering PrintCreditNote(creditNote={CreditNote})", creditNote);
            JObject prms;
            try
            {
                prms = BuildReceiptParams(creditNote, creditNote);
                var reversal = (JObject)prms["stornoInput"]!;
                if (string.IsNullOrEmpty(creditNote.OriginalInvoiceNumber))
                {
                    throw new StandardizedStatusMessageException(
                        "OriginalInvoiceNumber of the credit note is empty", "E405");
                }

                if (string.IsNullOrEmpty(creditNote.FiscalDeviceSerialNumber))
                {
                    throw new StandardizedStatusMessageException(
                        "FiscalDeviceSerialNumber of the original invoice is required by this device", "E405");
                }

                reversal["invoiceNumber"] = creditNote.OriginalInvoiceNumber;
                reversal["fiscDevNumber"] = creditNote.FiscalDeviceSerialNumber;
                prms["invoiceData"] = BuildInvoiceData(creditNote);
            }
            catch (StandardizedStatusMessageException e)
            {
			_logger.LogDebug("<<< Exiting PrintCreditNote(creditNote={CreditNote})", creditNote);
                return ErrorResult(e);
            }

            return ExecutePrint(prms, creditNote, creditNote.Number);
        }

        protected (ReceiptInfo, DeviceStatus) PrintFiscalReceipt(Receipt receipt, ReversalReceipt? reversal)
        {
			_logger.LogInformation(">>> Entering PrintFiscalReceipt(receipt={Receipt},reversal={Reversal})", receipt, reversal);
            JObject prms;
            try
            {
                prms = BuildReceiptParams(receipt, reversal);
            }
            catch (StandardizedStatusMessageException e)
            {
			_logger.LogDebug("<<< Exiting PrintFiscalReceipt(receipt={Receipt},reversal={Reversal})", receipt, reversal);
                return ErrorResult(e);
            }

            return ExecutePrint(prms, receipt, null);
        }

        /// <summary>
        /// Sends the built printReceipt request and resolves the returned receipt info. When
        /// <paramref name="invoiceNumber"/> is non-null the result is an <see cref="InvoiceInfo"/>
        /// carrying that number (invoice / credit note); otherwise a plain <see cref="ReceiptInfo"/>.
        /// </summary>
        protected (ReceiptInfo, DeviceStatus) ExecutePrint(JObject prms, Receipt receipt, string? invoiceNumber)
        {
			_logger.LogInformation(">>> Entering ExecutePrint(prms={Prms},receipt={Receipt},invoiceNumber={InvoiceNumber})", prms, receipt, invoiceNumber);
            var (json, status) = Request("printReceipt", prms);
            if (!status.Ok)
            {
                return (new ReceiptInfo(), status);
            }

            var (info, enrichedStatus) = EnrichReceiptInfo(json, status, receipt);

            if (invoiceNumber != null)
            {
                info = new InvoiceInfo
                {
                    ReceiptNumber = info.ReceiptNumber,
                    ReceiptDateTime = info.ReceiptDateTime,
                    ReceiptAmount = info.ReceiptAmount,
                    FiscalMemorySerialNumber = info.FiscalMemorySerialNumber,
                    InvoiceNumber = invoiceNumber
                };
            }

			_logger.LogDebug("<<< Exiting ExecutePrint(prms={Prms},receipt={Receipt},invoiceNumber={InvoiceNumber})", prms, receipt, invoiceNumber);
            return (info, enrichedStatus);
        }

        private static (ReceiptInfo, DeviceStatus) ErrorResult(StandardizedStatusMessageException e)
        {
			_logger.LogInformation(">>> Entering ErrorResult(e={E})", e);
            var status = new DeviceStatus();
            status.AddError(e.Code, e.Message);
			_logger.LogDebug("<<< Exiting ErrorResult(e={E})", e);
            return (new ReceiptInfo(), status);
        }

        /// <summary>
        /// After a successful fiscal receipt, resolves the exact receipt number, amount, date and fiscal
        /// memory number. The printReceipt answer already carries this inline as a "qrcode" field, so it is
        /// used directly (no extra round-trip). If it is missing, the last QR code is read separately, and
        /// as a final fallback the plain printReceipt answer fields are used.
        /// </summary>
        protected (ReceiptInfo, DeviceStatus) EnrichReceiptInfo(JObject printResponse, DeviceStatus status, Receipt receipt)
        {
			_logger.LogInformation(">>> Entering EnrichReceiptInfo(printResponse={PrintResponse},status={Status},receipt={Receipt})", printResponse, status, receipt);
            // Preferred: the QR code returned inline with the printReceipt answer (no extra round-trip).
            if (ParseQrCode(printResponse.Value<string>("qrcode")) is ReceiptInfo inlineInfo)
            {
                return (inlineInfo, status);
            }

            // Fallback: read the last QR code explicitly.
            var (qrJson, qrStatus) = Request(
                "getData",
                null,
                new JObject { ["period"] = "day", ["type"] = "LastQRCode" });

            if (qrStatus.Ok && ParseQrCode(qrJson["response"]?.Value<string>("data")) is ReceiptInfo qrInfo)
            {
                return (qrInfo, status);
            }

            // Final fallback: the plain printReceipt answer fields.
            var fallback = new ReceiptInfo
            {
                FiscalMemorySerialNumber = Info.FiscalMemorySerialNumber,
                ReceiptNumber = printResponse.Value<string>("grandReceiptNum") ?? string.Empty,
                ReceiptAmount = ComputeTotal(receipt)
            };

            var ts = printResponse.Value<string>("receiptTimestamp");
            if (!string.IsNullOrEmpty(ts) && TryParseReceiptTimestamp(ts!, out var dt))
            {
                fallback.ReceiptDateTime = dt;
            }

			_logger.LogDebug("<<< Exiting EnrichReceiptInfo(printResponse={PrintResponse},status={Status},receipt={Receipt})", printResponse, status, receipt);
            return (fallback, status);
        }

        /// <summary>
        /// Parses a QR code payload in the format FM*number*date*time*amount
        /// (e.g. 51019395*0000000201*2024-11-25*11:04:30*4.75) into a <see cref="ReceiptInfo"/>.
        /// Returns null when the payload is empty or does not have the expected structure.
        /// </summary>
        protected static ReceiptInfo? ParseQrCode(string? qrCode)
        {
			_logger.LogInformation(">>> Entering ParseQrCode(qrCode={QrCode})", qrCode);
            if (string.IsNullOrEmpty(qrCode))
            {
                return null;
            }

            var fields = qrCode.Split('*');
            if (fields.Length < 5)
            {
                return null;
            }

            var info = new ReceiptInfo
            {
                FiscalMemorySerialNumber = fields[0],
                ReceiptNumber = fields[1]
            };

            if (DateTime.TryParseExact($"{fields[2]} {fields[3]}", "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var receiptDateTime))
            {
                info.ReceiptDateTime = receiptDateTime;
            }

            if (decimal.TryParse(fields[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var receiptAmount))
            {
                info.ReceiptAmount = receiptAmount;
            }

			_logger.LogDebug("<<< Exiting ParseQrCode(qrCode={QrCode})", qrCode);
            return info;
        }

        public override DeviceStatus PrintZReport(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering PrintZReport(credentials=<redacted>)");
            var (_, status) = Request("printZReport");
			_logger.LogDebug("<<< Exiting PrintZReport(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatus PrintXReport(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering PrintXReport(credentials=<redacted>)");
            var (_, status) = Request("printXReport");
			_logger.LogDebug("<<< Exiting PrintXReport(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatus PrintPeriodReport(PeriodReport periodReport)
        {
			_logger.LogInformation(">>> Entering PrintPeriodReport(periodReport={PeriodReport})", periodReport);
            // "type" and "data" go at the root next to method/id, like getData - not under params.
            // FD2D is the full (detailed) and SD2D the short report from date to date; the two dates
            // travel together in "data" as "DD/MM/YY, DD/MM/YY".
            var (_, status) = Request(
                "printMFReport",
                null,
                new JObject
                {
                    ["type"] = periodReport.Type == PeriodReportType.Detailed ? "FD2D" : "SD2D",
                    ["data"] =
                        $"{FormatPeriodReportDate(periodReport.StartDate)}, " +
                        $"{FormatPeriodReportDate(periodReport.EndDate)}"
                });

			_logger.LogDebug("<<< Exiting PrintPeriodReport(periodReport={PeriodReport})", periodReport);
            return status;
        }

        public override DeviceStatus PrintDuplicate(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering PrintDuplicate(credentials=<redacted>)");
            var (_, status) = Request("printDuplicate");
			_logger.LogDebug("<<< Exiting PrintDuplicate(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatusWithRawResponse RawRequest(RequestFrame requestFrame)
        {
			_logger.LogInformation(">>> Entering RawRequest(requestFrame={RequestFrame})", requestFrame);
            var status = new DeviceStatus();
            var raw = string.Empty;
            JObject request;
            try
            {
                request = JObject.Parse(requestFrame.RawRequest);
            }
            catch
            {
                status.AddError("E401", "RawRequest must be a valid JSON-RPC object");
                return new DeviceStatusWithRawResponse(status) { RawResponse = raw };
            }

            try
            {
                JObject json;
                (json, raw) = RawJsonRequest(request);
                status = ParseResponseStatus(json);
            }
            catch (Exception ex)
            {
                status.AddError("E999", ex.Message);
            }

            return new DeviceStatusWithRawResponse(status) { RawResponse = raw };
        }

        public override DeviceStatusWithDateTime Reset(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering Reset(credentials=<redacted>)");
            // Per the SIS spec, getError cancels any pending/open receipt on the device.
            Request("getError");
			_logger.LogDebug("<<< Exiting Reset(credentials=<redacted>)");
            return CheckStatus();
        }
    }
}
