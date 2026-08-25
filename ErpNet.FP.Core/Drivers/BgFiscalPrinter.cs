namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using ErpNet.FP.Core.Configuration;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fiscal printer base class for Bg printers.
    /// </summary>
    /// <seealso cref="ErpNet.FP.IFiscalPrinter" />
    public abstract class BgFiscalPrinter : IFiscalPrinter
    {
	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        protected readonly object frameSyncLock = new object();

        protected Encoding PrinterEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1251)
            ?? throw new NotSupportedException("Encoding 1251 (Cyrillic) is missing. Register CodePagesEncodingProvider!");

        public DeviceInfo Info = new DeviceInfo();

        public IDictionary<PaymentType, string> PaymentTypeMappings = new Dictionary<PaymentType, string>();        

        protected BgFiscalPrinter(
            IChannel channel, 
            ServiceOptions serviceOptions, 
            IDictionary<string, string>? options = null)
        {
			_logger.LogDebug(">>> Entering BgFiscalPrinter(channel={Channel},serviceOptions={ServiceOptions},options={Options})", channel, serviceOptions, options);
            ServiceOptions = serviceOptions;
            Options = new Dictionary<string, string>()
                .MergeWith(GetDefaultOptions())
                .MergeWith(options);            
				_logger.LogDebug("BgFiscalPrinter(channel={Channel},serviceOptions={ServiceOptions},options={Options}): ServiceOptions → {ServiceOptions1}", channel, serviceOptions, options, ServiceOptions);
				_logger.LogDebug("BgFiscalPrinter(channel={Channel},serviceOptions={ServiceOptions},options={Options}): Options → {Options1}", channel, serviceOptions, options, Options);
            Channel = channel;
        }

        protected abstract DeviceStatus ParseStatus(byte[]? status);

        protected IChannel Channel { get; }
        protected IDictionary<string, string> Options { get; }
        public ServiceOptions ServiceOptions { get; }

        public abstract DeviceStatusWithCashAmount Cash(Credentials credentials);

        public abstract DeviceStatusWithDateTime CheckStatus();

        public virtual IDictionary<string, string>? GetDefaultOptions()
        {
            return null;
        }

        public abstract IDictionary<PaymentType, string> GetPaymentTypeMappings();

        public string GetPaymentTypeText(PaymentType paymentType)
        {
			_logger.LogDebug(">>> Entering GetPaymentTypeText(paymentType={PaymentType})", paymentType);
            if (PaymentTypeMappings.TryGetValue(paymentType, out string? value))
            {
                if (value != null)
                {
                    return value;
                }
            }
			_logger.LogWarning("Throwing StandardizedStatusMessageException from GetPaymentTypeText(paymentType={PaymentType})", paymentType);
			_logger.LogDebug("<<< Exiting GetPaymentTypeText(paymentType={PaymentType})", paymentType);
            throw new StandardizedStatusMessageException($"Payment type {paymentType} unsupported", "E406");
        }

        public virtual string GetReversalReasonText(ReversalReason reversalReason)
        {
			_logger.LogDebug(">>> Entering GetReversalReasonText(reversalReason={ReversalReason})", reversalReason);
			_logger.LogDebug("GetReversalReasonText(reversalReason={ReversalReason}): ReversalReason.OperatorError → {Arg1}", reversalReason, ReversalReason.OperatorError);
			_logger.LogDebug("<<< Exiting GetReversalReasonText(reversalReason={ReversalReason})", reversalReason);
            return reversalReason switch
            {
                ReversalReason.OperatorError => "0",
                ReversalReason.Refund => "1",
                ReversalReason.TaxBaseReduction => "2",
                _ => "0",
            };
        }

        public ICollection<PaymentType> GetSupportedPaymentTypes()
        {
			_logger.LogDebug(">>> Entering GetSupportedPaymentTypes()");
            PaymentTypeMappings = GetPaymentTypeMappings();
			_logger.LogDebug("GetSupportedPaymentTypes(): PaymentTypeMappings → {PaymentTypeMappings}", PaymentTypeMappings);
            return PaymentTypeMappings.Keys;
        }

        public abstract string GetTaxGroupText(TaxGroup taxGroup);

        public abstract DeviceStatus PrintMoneyDeposit(TransferAmount transferAmount);

        public abstract DeviceStatus PrintMoneyWithdraw(TransferAmount transferAmount);

        public abstract (ReceiptInfo, DeviceStatus) PrintReceipt(Receipt receipt);

        public abstract (ReceiptInfo, DeviceStatus) PrintReversalReceipt(ReversalReceipt reversalReceipt);

        public abstract DeviceStatus PrintXReport(Credentials credentials);

        public abstract DeviceStatus PrintZReport(Credentials credentials);

        public abstract DeviceStatus PrintDuplicate(Credentials credentials);

        public abstract DeviceStatusWithRawResponse RawRequest(RequestFrame requestFrame);

        public abstract DeviceStatusWithDateTime Reset(Credentials credentials);

        public abstract DeviceStatus SetDateTime(CurrentDateTime currentDateTime);

        private static readonly Regex UniqueSalesNumberPattern =
            new Regex("^[A-Z]{2}[0-9]{6}-[A-Z0-9]{4}-[0-9]{7}$", RegexOptions.Compiled);

        public virtual DeviceStatus ValidateReceipt(Receipt receipt)
        {
			_logger.LogDebug(">>> Entering ValidateReceipt(receipt={Receipt})", receipt);
            var status = new DeviceStatus();
            if (receipt.Items == null || receipt.Items.Count == 0)
            {
                status.AddError("E410", "Receipt is empty, no items");
                return status;
            }
            if (String.IsNullOrEmpty(receipt.UniqueSaleNumber))
            {
                // NOTE: don't enforce USN
                // Let the device itself determine
            }
            else
            {
                var isMatch = UniqueSalesNumberPattern.IsMatch(receipt.UniqueSaleNumber);
				_logger.LogDebug("ValidateReceipt(receipt={Receipt}): isMatch → {IsMatch}", receipt, isMatch);
                if (!isMatch)
                {
                    status.AddError("E405", "Invalid format of UniqueSaleNumber");
                    return status;
                }
            }
            var itemsTotalAmount = 0.00m;
            var row = 0;
			_logger.LogDebug("ValidateReceipt(receipt={Receipt}): row → {Row}", receipt, row);
            foreach (var item in receipt.Items)
            {
                row++;

                switch (item.Type)
                {
                    case ItemType.Sale:
                        if (String.IsNullOrEmpty(item.Text))
                        {
                            status.AddError("E407", $"Item {row}: \"text\" is empty");
                        }
                        if (item.PriceModifierValue <= 0 && item.PriceModifierType != PriceModifierType.None)
                        {
                            status.AddError("E403", $"Item {row}: \"priceModifierValue\" should be positive number");
                        }
                        if (item.PriceModifierValue != 0 && item.PriceModifierType == PriceModifierType.None)
                        {
                            status.AddError("E403", $"Item {row}: \"priceModifierValue\" should'nt be \"none\" or empty. You can avoid setting priceModifier if you do not want price modification");
                        }
                        if (item.Quantity < 0)
                        {
                            status.AddError("E403", $"Item {row}: \"quantity\" should be positive number");
                        }
                        if (item.Department < 0)
                        {
                            status.AddError("E403", $"Item {row}; \"department\" should be positive number or zero.");
                        }
                        if (item.TaxGroup == TaxGroup.Unspecified)
                        {
                            status.AddError("E403", $"Item {row}: \"taxGroup\" shouldn't be \"unspecified\" or empty");
                        }
                        try
                        {
                            GetTaxGroupText(item.TaxGroup);
                        }
                        catch (StandardizedStatusMessageException e)
                        {
							_logger.LogError("Exception in ValidateReceipt(receipt={Receipt}): {Arg1}", receipt, e.Message);
                            status.AddError(e.Code, e.Message);
                        }
                        var quantity = Math.Round(item.Quantity == 0m ? 1m : item.Quantity, 3, MidpointRounding.AwayFromZero);
						_logger.LogDebug("ValidateReceipt(receipt={Receipt}): quantity → {Quantity}", receipt, quantity);
                        var unitPrice = Math.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero);
						_logger.LogDebug("ValidateReceipt(receipt={Receipt}): unitPrice → {UnitPrice}", receipt, unitPrice);
                        var itemPrice = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
						_logger.LogDebug("ValidateReceipt(receipt={Receipt}): itemPrice → {ItemPrice}", receipt, itemPrice);
                        var itemPriceModifierValue = Math.Round(item.PriceModifierValue, 2, MidpointRounding.AwayFromZero);
						_logger.LogDebug("ValidateReceipt(receipt={Receipt}): itemPriceModifierValue → {ItemPriceModifierValue}", receipt, itemPriceModifierValue);
                        switch (item.PriceModifierType)
                        {
                            case PriceModifierType.DiscountAmount:
                                itemPrice -= itemPriceModifierValue;
                                break;
                            case PriceModifierType.DiscountPercent:
                                itemPrice -= Math.Round(itemPrice * (itemPriceModifierValue / 100.0m), 2, MidpointRounding.AwayFromZero);
                                break;
                            case PriceModifierType.SurchargeAmount:
                                itemPrice += itemPriceModifierValue;
                                break;
                            case PriceModifierType.SurchargePercent:
                                itemPrice += Math.Round(itemPrice * (itemPriceModifierValue / 100.0m), 2, MidpointRounding.AwayFromZero);
                                break;
                        }
                        itemsTotalAmount += Math.Round(itemPrice, 2, MidpointRounding.AwayFromZero);
                        break;


                    case ItemType.Comment:
                        if (String.IsNullOrEmpty(item.Text))
                        {
                            status.AddError("E407", $"Item {row}: \"text\" is empty");
                        }
                        break;


                    case ItemType.FooterComment:
                        if (String.IsNullOrEmpty(item.Text))
                        {
                            status.AddError("E407", $"Item {row}: \"text\" is empty");
                        }
                        break;


                    case ItemType.SurchargeAmount:
                        if (item.Amount <= 0)
                        {
                            status.AddError("E403", $"Item {row}: \"amount\" should be positive number");
                        }
                        itemsTotalAmount += Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero);
                        break;


                    case ItemType.DiscountAmount:
                        if (item.Amount <= 0)
                        {
                            status.AddError("E403", $"Item {row}: \"amount\" should be positive number");
                        }
                        itemsTotalAmount -= Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero);
                        break;


                    default:
                        break;
                }

                if (!status.Ok)
                {
                    return status;
                }
            }
            if (receipt.Payments?.Count > 0)
            {
                var paymentAmount = 0.00m;
                row = 0;
                foreach (var payment in receipt.Payments)
                {
                    row++;

                    if (payment.Amount < 0 && payment.PaymentType != PaymentType.Change)
                    {
                        status.AddError("E403", $"Payment {row}: \"amount\" should be positive number or zero");
                    }
                    if (payment.Amount >= 0 && payment.PaymentType == PaymentType.Change)
                    {
                        status.AddError("E403", $"Change {row}: \"amount\" should be negative number");
                    }

                    try
                    {
                        if (payment.PaymentType != PaymentType.Change)
                        {
                            // Check if the payment type is supported
                            GetPaymentTypeText(payment.PaymentType);
                        }
                    }
                    catch (StandardizedStatusMessageException e)
                    {
                        status.AddError(e.Code, e.Message);
                    }

                    if (!status.Ok)
                    {
                        status.AddInfo($"Error occured at Payment {row}");
                        return status;
                    }

                    var amount = Math.Round(payment.Amount, 2, MidpointRounding.AwayFromZero);
					_logger.LogWarning("ValidateReceipt(receipt={Receipt}): amount → {Amount}", receipt, amount);
                    paymentAmount += amount;
                }
                var difference = Math.Abs(paymentAmount - itemsTotalAmount);
				_logger.LogDebug("ValidateReceipt(receipt={Receipt}): difference → {Difference}", receipt, difference);
                if (difference >= 0.01m && difference / itemsTotalAmount > 0.00001m)
                {
                    status.AddError("E403", $"Payment total amount ({paymentAmount.ToString(CultureInfo.InvariantCulture)}) should be the same as the items total amount ({itemsTotalAmount.ToString(CultureInfo.InvariantCulture)})");
                }
            }
			_logger.LogDebug("<<< Exiting ValidateReceipt(receipt={Receipt})", receipt);
            return status;
        }

        public virtual DeviceStatus ValidateReversalReceipt(ReversalReceipt reversalReceipt)
        {
			_logger.LogDebug(">>> Entering ValidateReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
            var status = ValidateReceipt(reversalReceipt);
            if (!status.Ok)
            {
                return status;
            }
            if (reversalReceipt.Payments?.Count > 0)
            {
                status.AddWarning("W302", "Reversal receipt payments array should be empty. It will be ignored.");
                reversalReceipt.Payments.Clear();
            }
            if (String.IsNullOrEmpty(reversalReceipt.ReceiptNumber))
            {
                status.AddError("E405", $"ReceiptNumber of the original receipt is empty");
                return status;
            }
            if (String.IsNullOrEmpty(reversalReceipt.FiscalMemorySerialNumber))
            {
                status.AddError("E405", $"FiscalMemorySerialNumber of the original receipt is empty");
			_logger.LogDebug("<<< Exiting ValidateReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
                return status;
            }
            return status;
        }

        // Native invoice / credit note. The default implementations report "not implemented".
        public virtual DeviceStatus ValidateInvoice(Invoice invoice) => NotImplemented("Invoice");

        public virtual DeviceStatus ValidateCreditNote(CreditNote creditNote) => NotImplemented("Credit note");

        public virtual (ReceiptInfo, DeviceStatus) PrintInvoice(Invoice invoice)
            => (new ReceiptInfo(), NotImplemented("Invoice"));

        public virtual (ReceiptInfo, DeviceStatus) PrintCreditNote(CreditNote creditNote)
            => (new ReceiptInfo(), NotImplemented("Credit note"));

        // Fiscal memory report for a custom period. The default implementation reports "not implemented".
        public virtual DeviceStatus PrintPeriodReport(PeriodReport periodReport)
            => NotImplemented("Period report");

        public virtual DeviceStatus ValidatePeriodReport(PeriodReport periodReport)
        {
			_logger.LogDebug(">>> Entering ValidatePeriodReport(periodReport={PeriodReport})", periodReport);
            if (!DeviceInfo.SupportsPeriodReport)
            {
			_logger.LogDebug("<<< Exiting ValidatePeriodReport(periodReport={PeriodReport})", periodReport);
                return NotImplemented("Period report");
            }

            return ValidatePeriodReportBase(periodReport);
        }

        /// <summary>
        /// Reusable period report validation for drivers that implement period reports.
        /// Only the bounds themselves are checked here - everything else is left to the device,
        /// so that its own rejection reaches the caller instead of being pre-empted.
        /// </summary>
        protected static DeviceStatus ValidatePeriodReportBase(PeriodReport periodReport)
        {
			_logger.LogDebug(">>> Entering ValidatePeriodReportBase(periodReport={PeriodReport})", periodReport);
            var status = new DeviceStatus();
            if (periodReport.StartDate == DateTime.MinValue)
            {
                status.AddError("E405", "StartDate of the period report is empty");
                return status;
            }

            if (periodReport.EndDate == DateTime.MinValue)
            {
                status.AddError("E405", "EndDate of the period report is empty");
                return status;
            }

            if (periodReport.StartDate.Date > periodReport.EndDate.Date)
            {
                status.AddError("E403", "StartDate of the period report is after its EndDate");
			_logger.LogDebug("<<< Exiting ValidatePeriodReportBase(periodReport={PeriodReport})", periodReport);
                return status;
            }

            return status;
        }

        protected static DeviceStatus NotImplemented(string documentName)
        {
			_logger.LogDebug(">>> Entering NotImplemented(documentName={DocumentName})", documentName);
            var status = new DeviceStatus();
            status.AddError("E413", $"{documentName} printing is not implemented by this driver");
			_logger.LogDebug("NotImplemented(documentName={DocumentName}): status → {Status}", documentName, status);
			_logger.LogDebug("<<< Exiting NotImplemented(documentName={DocumentName})", documentName);
            return status;
        }

        /// <summary>
        /// Reusable invoice validation for drivers that implement invoices.
        /// </summary>
        protected DeviceStatus ValidateInvoiceCore(Invoice invoice)
        {
			_logger.LogDebug(">>> Entering ValidateInvoiceCore(invoice={Invoice})", invoice);
            var status = ValidateReceipt(invoice);
            if (!status.Ok)
            {
                return status;
            }

            ValidateInvoiceDocument(status, invoice, Info.InvoiceNumberAssignment, "Invoice");

			_logger.LogDebug("<<< Exiting ValidateInvoiceCore(invoice={Invoice})", invoice);
            return status;
        }

        /// <summary>
        /// Reusable credit note validation for drivers that implement credit notes.
        /// </summary>
        protected DeviceStatus ValidateCreditNoteCore(CreditNote creditNote)
        {
			_logger.LogDebug(">>> Entering ValidateCreditNoteCore(creditNote={CreditNote})", creditNote);
            var status = ValidateReversalReceipt(creditNote);
            if (!status.Ok)
            {
                return status;
            }

            ValidateInvoiceDocument(status, creditNote, Info.CreditNoteNumberAssignment, "Credit note");

            if (string.IsNullOrEmpty(creditNote.OriginalInvoiceNumber))
            {
                status.AddError("E405", "OriginalInvoiceNumber of the credit note is empty");
            }

			_logger.LogDebug("<<< Exiting ValidateCreditNoteCore(creditNote={CreditNote})", creditNote);
            return status;
        }

        /// <summary>
        /// Validates the shared invoice-mode fields (recipient block and number assignment).
        /// </summary>
        protected void ValidateInvoiceDocument(
            DeviceStatus status,
            IInvoiceDocument document,
            NumberAssignment numberAssignment,
            string documentName)
        {
			_logger.LogDebug(">>> Entering ValidateInvoiceDocument(status={Status},document={Document},numberAssignment={NumberAssignment},documentName={DocumentName})", status, document, numberAssignment, documentName);
            var recipient = document.Recipient;
            if (recipient == null)
            {
                status.AddError("E405", $"{documentName} requires a \"recipient\"");
                return;
            }

            if (string.IsNullOrEmpty(recipient.Name))
            {
                status.AddError("E405", "Recipient \"name\" is empty");
            }

            if (string.IsNullOrEmpty(recipient.Identifier))
            {
                status.AddError("E405", "Recipient \"identifier\" is empty");
            }

            if (recipient.IdentifierType == IdentifierType.Unspecified)
            {
                status.AddError("E405", "Recipient \"identifierType\" is unspecified");
            }

            if (string.IsNullOrEmpty(recipient.Address))
            {
                status.AddError("E405", "Recipient \"address\" is empty");
            }

            if (string.IsNullOrEmpty(document.Number))
            {
                if (numberAssignment == NumberAssignment.ExternalRequired)
                {
                    status.AddError("E405", $"{documentName} \"number\" is required by this device");
                }
            }
            else if (numberAssignment == NumberAssignment.DeviceAssigned)
            {
                status.AddError("E412", $"External {documentName.ToLowerInvariant()} number is not supported by this device");
            }
			_logger.LogDebug("<<< Exiting ValidateInvoiceDocument(status={Status},document={Document},numberAssignment={NumberAssignment},documentName={DocumentName})", status, document, numberAssignment, documentName);
        }



        public virtual DeviceStatus ValidateTransferAmount(TransferAmount transferAmount)
        {
			_logger.LogDebug(">>> Entering ValidateTransferAmount(transferAmount={TransferAmount})", transferAmount);
            var status = new DeviceStatus();
            if (transferAmount.Amount <= 0)
            {
                status.AddError("E403", "Amount should be positive number");
            }
			_logger.LogDebug("<<< Exiting ValidateTransferAmount(transferAmount={TransferAmount})", transferAmount);
            return status;
        }

        public DeviceInfo DeviceInfo => Info;

        public void SetDeadLine(DateTime deadLine) => DeadLine = deadLine;
        public DateTime DeadLine { get; private set; } = DateTime.MaxValue;
    }
}
