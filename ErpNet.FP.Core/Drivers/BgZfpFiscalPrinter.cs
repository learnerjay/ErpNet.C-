namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using ErpNet.FP.Core.Configuration;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fiscal printer using the Zfp implementation.
    /// </summary>
    /// <seealso cref="ErpNet.FP.Core.Drivers.BgFiscalPrinter" />
    public partial class BgZfpFiscalPrinter : BgFiscalPrinter
    {
        public BgZfpFiscalPrinter(
            IChannel channel, 
            ServiceOptions serviceOptions, 
            IDictionary<string, string>? options = null)
        : base(channel, serviceOptions, options) { }

        public override string GetTaxGroupText(TaxGroup taxGroup)
        {
			_logger.LogInformation(">>> Entering GetTaxGroupText(taxGroup={TaxGroup})", taxGroup);
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

        public override IDictionary<PaymentType, string> GetPaymentTypeMappings()
        {
            var paymentTypeMappings = new Dictionary<PaymentType, string> {
                { PaymentType.Cash,          "0" },
                { PaymentType.Check,         "1" },
                { PaymentType.Coupons,       "2" },
                { PaymentType.ExtCoupons,    "3" },
                { PaymentType.Packaging,     "4" },
                { PaymentType.InternalUsage, "5" },
                { PaymentType.Damage,        "6" },
                { PaymentType.Card,          "7" },
                { PaymentType.Bank,          "8" },
                { PaymentType.Reserved1,     "9" },
                { PaymentType.Reserved2,    "10" }
            };
            ServiceOptions.RemapPaymentTypes(Info.SerialNumber, paymentTypeMappings);
            return paymentTypeMappings;
        }

        public override DeviceStatusWithDateTime CheckStatus()
        {
			_logger.LogInformation(">>> Entering CheckStatus()");
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

        public override DeviceStatus SetDateTime(CurrentDateTime currentDateTime)
        {
			_logger.LogInformation(">>> Entering SetDateTime(currentDateTime={CurrentDateTime})", currentDateTime);
            var (_, status) = SetDeviceDateTime(currentDateTime.DeviceDateTime);
			_logger.LogDebug("<<< Exiting SetDateTime(currentDateTime={CurrentDateTime})", currentDateTime);
            return status;
        }

        public override DeviceStatus PrintMoneyDeposit(TransferAmount transferAmount)
        {
			_logger.LogInformation(">>> Entering PrintMoneyDeposit(transferAmount={TransferAmount})", transferAmount);
            var (_, status) = MoneyTransfer(transferAmount);
			_logger.LogDebug("<<< Exiting PrintMoneyDeposit(transferAmount={TransferAmount})", transferAmount);
            return status;
        }

        public override DeviceStatus PrintMoneyWithdraw(TransferAmount transferAmount)
        {
			_logger.LogInformation(">>> Entering PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            if (transferAmount.Amount < 0m)
            {
				_logger.LogError("Throwing StandardizedStatusMessageException from PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
                throw new StandardizedStatusMessageException("Withdraw amount must be positive number", "E403");
            }
            transferAmount.Amount = -transferAmount.Amount;
			_logger.LogDebug("PrintMoneyWithdraw(transferAmount={TransferAmount}): transferAmount.Amount → {Arg1}", transferAmount, transferAmount.Amount);
            var (_, status) = MoneyTransfer(transferAmount);
			_logger.LogDebug("<<< Exiting PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            return status;
        }

        protected virtual (ReceiptInfo, DeviceStatus) PrintReceiptBody(Receipt receipt)
        {
			_logger.LogInformation(">>> Entering PrintReceiptBody(receipt={Receipt})", receipt);
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
                                item.PriceModifierType);
                        }
                        catch (StandardizedStatusMessageException e)
                        {
							_logger.LogError("Exception in PrintReceiptBody(receipt={Receipt}): {Arg1}", receipt, e.Message);
                            deviceStatus = new DeviceStatus();
                            deviceStatus.AddError(e.Code, e.Message);
							_logger.LogDebug("PrintReceiptBody(receipt={Receipt}): deviceStatus → {DeviceStatus}", receipt, deviceStatus);
                        }
                    }
                    if (!deviceStatus.Ok)
                    {
                        AbortReceipt();
                        deviceStatus.AddInfo($"Error occurred in Item {itemNumber}");
                        return (receiptInfo, deviceStatus);
                    }
                    else if (item.Type == ItemType.SurchargeAmount)
                    {
                        (_, deviceStatus) = SubtotalChangeAmount(item.Amount);
                    }
                    else if (item.Type == ItemType.DiscountAmount)
                    {
                        (_, deviceStatus) = SubtotalChangeAmount(-item.Amount);
                    }
                }

            // Receipt payments
            if (receipt.Payments == null || receipt.Payments.Count == 0)
            {
                (_, deviceStatus) = FullPaymentAndCloseReceipt();
                if (!deviceStatus.Ok)
                {
                    AbortReceipt();
                    deviceStatus.AddInfo($"Error occurred while making full payment in cash and closing the receipt");
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
                        AbortReceipt();
                        deviceStatus.AddInfo($"Error occurred in Payment {paymentNumber}");
                        return (receiptInfo, deviceStatus);
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

                (_, deviceStatus) = CloseReceipt();
                if (!deviceStatus.Ok)
                {
                    AbortReceipt();
                    deviceStatus.AddInfo($"Error occurred while closing the receipt");
                    return (receiptInfo, deviceStatus);
                }
            }

			_logger.LogDebug("<<< Exiting PrintReceiptBody(receipt={Receipt})", receipt);
            return GetLastReceiptInfo();
        }

        public override (ReceiptInfo, DeviceStatus) PrintReversalReceipt(ReversalReceipt reversalReceipt)
        {
			_logger.LogInformation(">>> Entering PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
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
                return (new ReceiptInfo(), deviceStatus);
            }

            ReceiptInfo receiptInfo;
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
			_logger.LogInformation(">>> Entering PrintReceipt(receipt={Receipt})", receipt);
            // Abort all unfinished or erroneus receipts
            AbortReceipt();

            // Receipt header
            var (_, deviceStatus) = OpenReceipt(
                receipt.UniqueSaleNumber,
                receipt.Operator,
                receipt.OperatorPassword
            );
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while opening new fiscal receipt");
                return (new ReceiptInfo(), deviceStatus);
            }

            ReceiptInfo receiptInfo;
            (receiptInfo, deviceStatus) = PrintReceiptBody(receipt);
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while printing receipt items");
            }

			_logger.LogDebug("<<< Exiting PrintReceipt(receipt={Receipt})", receipt);
            return (receiptInfo, deviceStatus);
        }

        protected virtual (ReceiptInfo, DeviceStatus) GetLastReceiptInfo()
        {
			_logger.LogInformation(">>> Entering GetLastReceiptInfo()");
            // QR Code Data Format: <FM Number>*<Receipt Number>*<Receipt Date>*<Receipt Hour>*<Receipt Amount>
            // 50163145*000002*2020-01-28*15:29:00*30.00
            var (qrCodeData, deviceStatus) = GetLastReceiptQRCodeData();
            if (!deviceStatus.Ok)
            {
                deviceStatus.AddInfo($"Error occurred while reading last receipt QR code data");
                return (new ReceiptInfo(), deviceStatus);
            }

            var qrCodeFields = qrCodeData.Split('*');
			_logger.LogDebug("GetLastReceiptInfo(): qrCodeFields → {QrCodeFields}", qrCodeFields);
            decimal receiptAmount;
            try
            {
                receiptAmount = decimal.Parse(qrCodeFields[4], CultureInfo.InvariantCulture);
            }
            catch
            {
                deviceStatus.AddInfo($"Error occurred while parsing last receipt QR code data (receipt amount)");
                return (new ReceiptInfo(), deviceStatus);
            }
            DateTime receiptDateTime;
            try
            {
                receiptDateTime = DateTime.ParseExact(string.Format(
                    $"{qrCodeFields[2]} {qrCodeFields[3]}"),
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture);
					_logger.LogDebug("GetLastReceiptInfo(): receiptDateTime → {ReceiptDateTime}", receiptDateTime);
            }
            catch
            {
                deviceStatus.AddInfo($"Error occurred while parsing last receipt QR code data (receipt date and time)");
                return (new ReceiptInfo(), deviceStatus);
            }
            string receiptNumber = qrCodeFields[1];
            if (String.IsNullOrWhiteSpace(receiptNumber))
            {
                deviceStatus.AddInfo($"Error occurred while reading last receipt number");
                deviceStatus.AddError("E409", $"Last receipt number is empty");
                return (new ReceiptInfo(), deviceStatus);
            }
			_logger.LogDebug("<<< Exiting GetLastReceiptInfo()");
            return (new ReceiptInfo
            {
                FiscalMemorySerialNumber = qrCodeFields[0],
                ReceiptAmount = receiptAmount,
                ReceiptNumber = receiptNumber,
                ReceiptDateTime = receiptDateTime
            }, deviceStatus);
        }

        public override DeviceStatusWithCashAmount Cash(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering Cash(credentials=<redacted>)");
            var (response, status) = Request(CommandReadDailyAvailableAmounts, "0");
            var statusEx = new DeviceStatusWithCashAmount(status);
			_logger.LogDebug("Cash(credentials=<redacted>): statusEx → {StatusEx}", statusEx);
            var commaFields = response.Split(';');
			_logger.LogDebug("Cash(credentials=<redacted>): commaFields → {CommaFields}", commaFields);
            if (commaFields.Length < 3)
            {
                statusEx.AddInfo("Error occured while reading cash amount");
                statusEx.AddError("E409", "Invalid format");
            }
            else
            {
                var amountString = commaFields[1].Trim();
				_logger.LogDebug("Cash(credentials=<redacted>): amountString → {AmountString}", amountString);
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

        public override DeviceStatus PrintZReport(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering PrintZReport(credentials=<redacted>)");
            var (_, status) = PrintDailyReport(true);
			_logger.LogDebug("<<< Exiting PrintZReport(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatus PrintXReport(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering PrintXReport(credentials=<redacted>)");
            var (_, status) = PrintDailyReport(false);
			_logger.LogDebug("<<< Exiting PrintXReport(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatus PrintPeriodReport(PeriodReport periodReport)
        {
			_logger.LogInformation(">>> Entering PrintPeriodReport(periodReport={PeriodReport})", periodReport);
            var (_, status) = PrintPeriodReport(
                periodReport.Type,
                periodReport.StartDate,
                periodReport.EndDate);

			_logger.LogDebug("<<< Exiting PrintPeriodReport(periodReport={PeriodReport})", periodReport);
            return status;
        }

        public override DeviceStatus PrintDuplicate(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering PrintDuplicate(credentials=<redacted>)");
            var (_, status) = Request(CommandPrintLastReceiptDuplicate);
			_logger.LogDebug("<<< Exiting PrintDuplicate(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatusWithDateTime Reset(Credentials credentials)
        {
			_logger.LogInformation(">>> Entering Reset(credentials=<redacted>)");
            AbortReceipt();
            FullPaymentAndCloseReceipt();
			_logger.LogDebug("<<< Exiting Reset(credentials=<redacted>)");
            return CheckStatus();
        }
    }
}
