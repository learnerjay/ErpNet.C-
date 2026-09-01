namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using ErpNet.FP.Core.Configuration;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fiscal printer using the ISL implementation.
    /// </summary>
    /// <seealso cref="ErpNet.FP.BgFiscalPrinter" />
    public abstract partial class BgIslFiscalPrinter : BgFiscalPrinter
    {
        protected BgIslFiscalPrinter(
            IChannel channel, 
            ServiceOptions serviceOptions, 
            IDictionary<string, string>? options = null)
        : base(channel, serviceOptions, options) { }

        public override DeviceStatusWithDateTime CheckStatus()
        {
			_logger.LogDebug(">>> Entering CheckStatus()");
            var (dateTime, status) = GetDateTime();
            var statusEx = new DeviceStatusWithDateTime(status);
			_logger.LogDebug("CheckStatus(): statusEx → {StatusEx}", statusEx);
            if (dateTime.HasValue)
            {
                statusEx.DeviceDateTime = dateTime.Value;
				_logger.LogDebug("CheckStatus(): statusEx.DeviceDateTime → {Arg0}", statusEx.DeviceDateTime);
            }
            else
            {
                statusEx.AddInfo("Error occured while reading current status");
                statusEx.AddError("E409", "Cannot read current date and time");
            }
			_logger.LogDebug("<<< Exiting CheckStatus()");
            return statusEx;
        }

        public override DeviceStatusWithCashAmount Cash(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering Cash(credentials=<redacted>)");
            var (response, status) = Request(CommandMoneyTransfer, "0");
            var statusEx = new DeviceStatusWithCashAmount(status);
			_logger.LogDebug("Cash(credentials=<redacted>): statusEx → {StatusEx}", statusEx);
            var commaFields = response.Split(',');
			_logger.LogDebug("Cash(credentials=<redacted>): commaFields → {CommaFields}", commaFields);
            if (commaFields.Length < 4)
            {
                statusEx.AddInfo("Error occured while reading cash amount");
                statusEx.AddError("E409", "Invalid format");
            }
            else
            {
                var amountString = commaFields[1];
                if (amountString.Contains("."))
                {
                    statusEx.Amount = decimal.Parse(amountString, CultureInfo.InvariantCulture);
                }
                else
                {
                    statusEx.Amount = decimal.Parse(amountString, CultureInfo.InvariantCulture) / 100m;
                }
            }
			_logger.LogDebug("<<< Exiting Cash(credentials=<redacted>)");
            return statusEx;
        }

        public override DeviceStatus SetDateTime(CurrentDateTime currentDateTime)
        {
			_logger.LogDebug(">>> Entering SetDateTime(currentDateTime={CurrentDateTime})", currentDateTime);
            var (_, status) = SetDeviceDateTime(currentDateTime.DeviceDateTime);
			_logger.LogDebug("<<< Exiting SetDateTime(currentDateTime={CurrentDateTime})", currentDateTime);
            return status;
        }

        public override string GetTaxGroupText(TaxGroup taxGroup)
        {
			_logger.LogDebug(">>> Entering GetTaxGroupText(taxGroup={TaxGroup})", taxGroup);
			_logger.LogDebug("GetTaxGroupText(taxGroup={TaxGroup}): TaxGroup.TaxGroup1 → {Arg1}", taxGroup, TaxGroup.TaxGroup1);
			_logger.LogDebug("<<< Exiting GetTaxGroupText(taxGroup={TaxGroup})", taxGroup);
            return taxGroup switch
            {
                TaxGroup.TaxGroup1 => "А",
                TaxGroup.TaxGroup2 => "Б",
                TaxGroup.TaxGroup3 => "В",
                TaxGroup.TaxGroup4 => "Г",
                TaxGroup.TaxGroup5 => "Д",
                TaxGroup.TaxGroup6 => "Е",
                TaxGroup.TaxGroup7 => "Ж",
                TaxGroup.TaxGroup8 => "З",
                _ => throw new StandardizedStatusMessageException($"Tax group {taxGroup} unsupported", "E411"),
            };
        }

        public override DeviceStatus PrintMoneyDeposit(TransferAmount transferAmount)
        {
			_logger.LogDebug(">>> Entering PrintMoneyDeposit(transferAmount={TransferAmount})", transferAmount);
            var (_, status) = MoneyTransfer(transferAmount.Amount);
			_logger.LogDebug("<<< Exiting PrintMoneyDeposit(transferAmount={TransferAmount})", transferAmount);
            return status;
        }

        public override DeviceStatus PrintMoneyWithdraw(TransferAmount transferAmount)
        {
			_logger.LogDebug(">>> Entering PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            if (transferAmount.Amount < 0m)
            {
				_logger.LogWarning("Throwing StandardizedStatusMessageException from PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
                throw new StandardizedStatusMessageException("Withdraw amount must be positive number", "E403");
            }
            var (_, status) = MoneyTransfer(-transferAmount.Amount);
			_logger.LogDebug("<<< Exiting PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            return status;
        }

        public virtual (ReceiptInfo, DeviceStatus) PrintReceiptBody(Receipt receipt)
        {
			_logger.LogDebug(">>> Entering PrintReceiptBody(receipt={Receipt})", receipt);
            var receiptInfo = new ReceiptInfo();

			_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): receiptInfo → {ReceiptInfo}", receipt, receiptInfo);
            var (fiscalMemorySerialNumber, deviceStatus) = GetFiscalMemorySerialNumber();
            if (!deviceStatus.Ok)
            {
                return (receiptInfo, deviceStatus);
            }

            receiptInfo.FiscalMemorySerialNumber = fiscalMemorySerialNumber;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): receiptInfo.FiscalMemorySerialNumber → {Arg1}", receipt, receiptInfo.FiscalMemorySerialNumber);

            uint itemNumber = 0;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): itemNumber → {ItemNumber}", receipt, itemNumber);
            // Receipt items
            if (receipt.Items != null) foreach (var item in receipt.Items)
                {
                    itemNumber++;
                    if (item.Type == ItemType.Comment)
                    {
                        (_, deviceStatus) = AddComment(item.Text);
                    }
                    else if (item.Type == ItemType.Sale)
                    {
                        try
                        {
                            (_, deviceStatus) = AddItem(
                                item.Department,
                                item.Text,
                                item.UnitPrice,
                                item.TaxGroup,
                                item.Quantity,
                                item.PriceModifierValue,
                                item.PriceModifierType,
                                item.ItemCode);
                        }
                        catch (StandardizedStatusMessageException e)
                        {
							_logger.LogError("Exception in PrintReceiptBody(receipt={Receipt}): {Arg1}", receipt, e.Message);
                            deviceStatus = new DeviceStatus();
                            deviceStatus.AddError(e.Code, e.Message);
							_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): deviceStatus → {DeviceStatus}", receipt, deviceStatus);
                            break;
                        }
                    }
                    else if (item.Type == ItemType.SurchargeAmount)
                    {
                        (_, deviceStatus) = SubtotalChangeAmount(item.Amount);
                    }
                    else if (item.Type == ItemType.DiscountAmount)
                    {                        
                        (_, deviceStatus) = SubtotalChangeAmount(-item.Amount);
                    }
                    if (!deviceStatus.Ok)
                    {
                        deviceStatus.AddInfo($"Error occurred in Item {itemNumber}");
                        return (receiptInfo, deviceStatus);
                    }
                }

            // Receipt payments
            if (receipt.Payments == null || receipt.Payments.Count == 0)
            {
                (_, deviceStatus) = FullPayment();
                if (!deviceStatus.Ok)
                {
                    deviceStatus.AddInfo($"Error occurred while making full payment in cash");
                    return (receiptInfo, deviceStatus);
                }
            }
            else
            {
                uint paymentNumber = 0;
                foreach (var payment in receipt.Payments)
                {
                    paymentNumber++;

                    if (payment.PaymentType == PaymentType.Change)
                    {
                        // PaymentType.Change is abstract payment, 
                        // used only for computing the total sum of the payments.
                        // So we will skip it.
                        continue;
                    }

                    try
                    {
                        (_, deviceStatus) = AddPayment(payment.Amount, payment.PaymentType);
                    }
                    catch (StandardizedStatusMessageException e)
                    {
                        deviceStatus = new DeviceStatus();
                        deviceStatus.AddError(e.Code, e.Message);
                    }

                    if (!deviceStatus.Ok)
                    {
                        deviceStatus.AddInfo($"Error occurred in Payment {paymentNumber}");
                        return (receiptInfo, deviceStatus);
                    }
                }
            }

            itemNumber = 0;
            if (receipt.Items != null) foreach (var item in receipt.Items)
                {
                    itemNumber++;
                    if (item.Type == ItemType.FooterComment)
                    {
                        (_, deviceStatus) = AddComment(item.Text);
                        if (!deviceStatus.Ok)
                        {
                            deviceStatus.AddInfo($"Error occurred in Item {itemNumber}");
                            return (receiptInfo, deviceStatus);
                        }
                    }
                }

            // Get the receipt date and time (current fiscal device date and time)
            DateTime? dateTime;
            (dateTime, deviceStatus) = GetDateTime();
            if (!dateTime.HasValue || !deviceStatus.Ok)
            {
                AbortReceipt();
                return (receiptInfo, deviceStatus);
            }
            receiptInfo.ReceiptDateTime = dateTime.Value;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): receiptInfo.ReceiptDateTime → {Arg1}", receipt, receiptInfo.ReceiptDateTime);

            // Get receipt amount
            decimal? receiptAmount;
            (receiptAmount, deviceStatus) = GetReceiptAmount();
            if (!receiptAmount.HasValue || !deviceStatus.Ok)
            {
                AbortReceipt();
                return (receiptInfo, deviceStatus);
            }
            receiptInfo.ReceiptAmount = receiptAmount.Value;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): receiptInfo.ReceiptAmount → {Arg1}", receipt, receiptInfo.ReceiptAmount);

            // Closing receipt
            string closeReceiptResponse;
            (closeReceiptResponse, deviceStatus) = CloseReceipt();
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occurred while closing the receipt");
                return (receiptInfo, deviceStatus);
            }

            // Get receipt number
            string lastDocumentNumberResponse;
            (lastDocumentNumberResponse, deviceStatus) = GetLastDocumentNumber(closeReceiptResponse);
            if (!deviceStatus.Ok || String.IsNullOrWhiteSpace(lastDocumentNumberResponse))
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occurred while reading last receipt number");
                deviceStatus.AddError("E409", $"Last receipt number is empty");
                return (receiptInfo, deviceStatus);
            }
            receiptInfo.ReceiptNumber = lastDocumentNumberResponse;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): receiptInfo.ReceiptNumber → {Arg1}", receipt, receiptInfo.ReceiptNumber);

            if (DeviceInfo.SupportPaymentTerminal && DeviceInfo.UsePaymentTerminal && deviceStatus.Ok && receipt.Payments != null)
            {
                var payWithCard = false;
				_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): payWithCard → {PayWithCard}", receipt, payWithCard);
                foreach (var p in receipt.Payments)
                {
                    if (p.PaymentType == PaymentType.Card)
                        payWithCard = true;
                }
                // print client receipt for pinpad transaction after successfully closed fiscal note
                if (this is BgDatecs.BgDatecsXIslFiscalPrinter && payWithCard)
                    (_, _) = Request(CommandToPinpad, DatecsXPinpadPrintReceipt);
            }

			_logger.LogDebug("<<< Exiting PrintReceiptBody(receipt={Receipt})", receipt);
            return (receiptInfo, deviceStatus);
        }

        public override (ReceiptInfo, DeviceStatus) PrintReversalReceipt(ReversalReceipt reversalReceipt)
        {
			_logger.LogDebug(">>> Entering PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
            var receiptInfo = new ReceiptInfo();

			_logger.LogDebug("PrintReversalReceipt(reversalReceipt={ReversalReceipt}): receiptInfo → {ReceiptInfo}", reversalReceipt, receiptInfo);
            // Abort all unfinished or erroneus receipts
            AbortReceipt();

            // Receipt header
            var (_, deviceStatus) = OpenReversalReceipt(
                reversalReceipt.Reason,
                reversalReceipt.ReceiptNumber,
                reversalReceipt.ReceiptDateTime,
                reversalReceipt.FiscalMemorySerialNumber,
                reversalReceipt.UniqueSaleNumber,
                reversalReceipt.Operator,
                reversalReceipt.OperatorPassword);
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while opening new fiscal reversal receipt");
                return (receiptInfo, deviceStatus);
            }

            (receiptInfo, deviceStatus) = PrintReceiptBody(reversalReceipt);
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while printing receipt items");
            }

			_logger.LogDebug("<<< Exiting PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
            return (receiptInfo, deviceStatus);
        }

        public override (ReceiptInfo, DeviceStatus) PrintReceipt(Receipt receipt)
        {
			_logger.LogDebug(">>> Entering PrintReceipt(receipt={Receipt})", receipt);
            var receiptInfo = new ReceiptInfo();

			_logger.LogDebug("PrintReceipt(receipt={Receipt}): receiptInfo → {ReceiptInfo}", receipt, receiptInfo);
            // Abort all unfinished or erroneus receipts
            AbortReceipt();

            // Opening receipt
            var (_, deviceStatus) = OpenReceipt(
                receipt.UniqueSaleNumber,
                receipt.Operator,
                receipt.OperatorPassword
            );
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while opening new fiscal receipt");
                return (receiptInfo, deviceStatus);
            }

            // Printing receipt's body
            (receiptInfo, deviceStatus) = PrintReceiptBody(receipt);
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while printing receipt items");
            }

			_logger.LogDebug("<<< Exiting PrintReceipt(receipt={Receipt})", receipt);
            return (receiptInfo, deviceStatus);
        }

        public override DeviceStatus PrintZReport(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering PrintZReport(credentials=<redacted>)");
            var (_, status) = PrintDailyReport(true);
			_logger.LogDebug("<<< Exiting PrintZReport(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatus PrintXReport(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering PrintXReport(credentials=<redacted>)");
            var (_, status) = PrintDailyReport(false);
			_logger.LogDebug("<<< Exiting PrintXReport(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatus PrintPeriodReport(PeriodReport periodReport)
        {
			_logger.LogDebug(">>> Entering PrintPeriodReport(periodReport={PeriodReport})", periodReport);
            var (_, status) = PrintPeriodReport(
                periodReport.Type,
                periodReport.StartDate,
                periodReport.EndDate);

			_logger.LogDebug("<<< Exiting PrintPeriodReport(periodReport={PeriodReport})", periodReport);
            return status;
        }

        public override DeviceStatus PrintDuplicate(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering PrintDuplicate(credentials=<redacted>)");
            var (_, status) = Request(CommandPrintLastReceiptDuplicate, "1");
			_logger.LogDebug("<<< Exiting PrintDuplicate(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatusWithDateTime Reset(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering Reset(credentials=<redacted>)");
            AbortReceipt();
            FullPayment();
            CloseReceipt();
			_logger.LogDebug("<<< Exiting Reset(credentials=<redacted>)");
            return CheckStatus();
        }
    }
}
