namespace ErpNet.FP.Core.Drivers.BgIcp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using ErpNet.FP.Core.Configuration;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fiscal printer using the Icp implementation of Isl Bulgaria.
    /// </summary>
    /// <seealso cref="ErpNet.FP.Drivers.BgIcpFiscalPrinter" />
    public partial class BgIcpFiscalPrinter : BgFiscalPrinter
    {
        public BgIcpFiscalPrinter(
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
            var (response, status) = Request("F80D");
            var statusEx = new DeviceStatusWithCashAmount(status);
			_logger.LogDebug("Cash(credentials=<redacted>): statusEx → {StatusEx}", statusEx);
            var amountCash = response.Split(new int[] { 10 });
			_logger.LogDebug("Cash(credentials=<redacted>): amountCash → {AmountCash}", amountCash);
            if (amountCash.Length != 1)
            {
                statusEx.AddInfo("Error occured while parsing current cash amount");
                statusEx.AddError("E409", "Cannot parse current cash amount");
                return statusEx;
            }

            try
            {
                statusEx.Amount = decimal.Parse(amountCash[0], CultureInfo.InvariantCulture) / 100m;
            }
            catch
            {
				_logger.LogError("Exception in Cash(credentials=<redacted>): {Arg1}", ex.Message);
                statusEx.AddInfo("Error occured while parsing current cash amount");
                statusEx.AddError("E409", "Cannot parse current cash amount");
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
            var (_, status) = MoneyTransfer(-transferAmount.Amount);
			_logger.LogDebug("<<< Exiting PrintMoneyWithdraw(transferAmount={TransferAmount})", transferAmount);
            return status;
        }

        public virtual (ReceiptInfo, DeviceStatus) PrintReceiptBody(Receipt receipt, bool reversalReceipt = false)
        {
			_logger.LogDebug(">>> Entering PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt})", receipt, reversalReceipt);
            var receiptInfo = new ReceiptInfo();

			_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): receiptInfo → {ReceiptInfo}", receipt, reversalReceipt, receiptInfo);
            var (fiscalMemorySerialNumber, deviceStatus) = GetFiscalMemorySerialNumber();
            if (!deviceStatus.Ok)
            {
                return (receiptInfo, deviceStatus);
            }

            receiptInfo.FiscalMemorySerialNumber = fiscalMemorySerialNumber;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): receiptInfo.FiscalMemorySerialNumber → {Arg2}", receipt, reversalReceipt, receiptInfo.FiscalMemorySerialNumber);

            if (receipt.Items == null || receipt.Items.Count == 0)
            {
                deviceStatus.AddError("E410", "Receipt.Items must be not null or empty");
                return (receiptInfo, deviceStatus);
            }

            uint itemNumber = 0;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): itemNumber → {ItemNumber}", receipt, reversalReceipt, itemNumber);
            // Receipt items
            foreach (var item in receipt.Items)
            {
                itemNumber++;
                if (item.Type == ItemType.Comment)
                {
                    (_, deviceStatus) = AddComment(receipt.UniqueSaleNumber, item.Text);
                    if (!deviceStatus.Ok)
                    {
                        deviceStatus.AddInfo($"Error occurred in Item {itemNumber}");
                        return (receiptInfo, deviceStatus);
                    }
                }
                else if (item.Type == ItemType.Sale)
                {
                    if (item.PriceModifierValue < 0m)
                    {
						_logger.LogWarning("Throwing StandardizedStatusMessageException from PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt})", receipt, reversalReceipt);
                        throw new StandardizedStatusMessageException("PriceModifierValue amount must be positive number", "E403");
                    }
                    if (item.PriceModifierValue != 0m && item.PriceModifierType == PriceModifierType.None)
                    {
                        throw new StandardizedStatusMessageException("PriceModifierValue must be 0 if priceModifierType is None", "E403");
                    }
                    try
                    {
                        (_, deviceStatus) = AddItem(
                            receipt.UniqueSaleNumber,
                            item.Department,
                            item.Text,
                            item.UnitPrice,
                            item.TaxGroup,
                            item.Quantity,
                            item.PriceModifierValue,
                            item.PriceModifierType,
                            reversalReceipt,
                            item.ItemCode);
                    }
                    catch (StandardizedStatusMessageException e)
                    {
						_logger.LogError("Exception in PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): {Arg2}", receipt, reversalReceipt, e.Message);
                        deviceStatus = new DeviceStatus();
                        deviceStatus.AddError(e.Code, e.Message);
						_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): deviceStatus → {DeviceStatus}", receipt, reversalReceipt, deviceStatus);
                    }
                    if (!deviceStatus.Ok)
                    {
                        deviceStatus.AddInfo($"Error occurred in Item {itemNumber}");
                        return (receiptInfo, deviceStatus);
                    }
                }
            }

            // Get receipt number and amount
            string receiptNumber;
            decimal? receiptAmount;
            (receiptNumber, receiptAmount, deviceStatus) = GetReceiptNumberAndAmount();
            if (!deviceStatus.Ok)
            {
                return (receiptInfo, deviceStatus);
            }

            receiptInfo.ReceiptNumber = receiptNumber;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): receiptInfo.ReceiptNumber → {Arg2}", receipt, reversalReceipt, receiptInfo.ReceiptNumber);
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): receiptInfo.ReceiptAmount → {Arg2}", receipt, reversalReceipt, receiptInfo.ReceiptAmount);
            receiptInfo.ReceiptAmount = receiptAmount ?? 0m;

            // Receipt payments
            if (receipt.Payments == null || receipt.Payments.Count == 0)
            {
                deviceStatus = FullPayment();
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
            // Receipt items
            foreach (var item in receipt.Items)
            {
                itemNumber++;
                if (item.Type == ItemType.FooterComment)
                {
                    (_, deviceStatus) = AddComment(receipt.UniqueSaleNumber, item.Text);
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
            if (!deviceStatus.Ok)
            {
                return (receiptInfo, deviceStatus);
            }

            receiptInfo.ReceiptDateTime = dateTime ?? DateTime.Now;
			_logger.LogDebug("PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt}): receiptInfo.ReceiptDateTime → {Arg2}", receipt, reversalReceipt, receiptInfo.ReceiptDateTime);

            if (deviceStatus.Ok)
            {
                deviceStatus = FullPayment();
            }

			_logger.LogDebug("<<< Exiting PrintReceiptBody(receipt={Receipt},reversalReceipt={ReversalReceipt})", receipt, reversalReceipt);
            return (receiptInfo, deviceStatus);
        }

        public override (ReceiptInfo, DeviceStatus) PrintReceipt(Receipt receipt)
        {
			_logger.LogDebug(">>> Entering PrintReceipt(receipt={Receipt})", receipt);
            // Printing receipt's body
            var (receiptInfo, deviceStatus) = PrintReceiptBody(receipt);
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while printing receipt body");
			_logger.LogDebug("<<< Exiting PrintReceipt(receipt={Receipt})", receipt);
                return (receiptInfo, deviceStatus);
            }

            return (receiptInfo, deviceStatus);
        }

        public override (ReceiptInfo, DeviceStatus) PrintReversalReceipt(ReversalReceipt reversalReceipt)
        {
			_logger.LogDebug(">>> Entering PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
            var receiptInfo = new ReceiptInfo();

			_logger.LogDebug("PrintReversalReceipt(reversalReceipt={ReversalReceipt}): receiptInfo → {ReceiptInfo}", reversalReceipt, receiptInfo);
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

            (receiptInfo, deviceStatus) = PrintReceiptBody(reversalReceipt, true);
            if (!deviceStatus.Ok)
            {
                AbortReceipt();
                deviceStatus.AddInfo($"Error occured while printing reversal receipt body");
			_logger.LogDebug("<<< Exiting PrintReversalReceipt(reversalReceipt={ReversalReceipt})", reversalReceipt);
                return (receiptInfo, deviceStatus);
            }

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
            var (_, status) = Request("AA");
			_logger.LogDebug("<<< Exiting PrintDuplicate(credentials=<redacted>)");
            return status;
        }

        public override DeviceStatusWithRawResponse RawRequest(RequestFrame requestFrame)
        {
			_logger.LogDebug(">>> Entering RawRequest(requestFrame={RequestFrame})", requestFrame);
            if (requestFrame.RawRequest.Length < 2)
            {
                var deviceStatus = new DeviceStatus();
				_logger.LogDebug("RawRequest(requestFrame={RequestFrame}): deviceStatus → {DeviceStatus}", requestFrame, deviceStatus);
                deviceStatus.AddError("E401", "Request length must be at least 2 characters");
                return new DeviceStatusWithRawResponse(deviceStatus);
            }
            var (rawResponse, status) = Request(requestFrame.RawRequest);
            var deviceStatusWithRawResponse = new DeviceStatusWithRawResponse(status) { RawResponse = rawResponse };
			_logger.LogDebug("RawRequest(requestFrame={RequestFrame}): deviceStatusWithRawResponse → {DeviceStatusWithRawResponse}", requestFrame, deviceStatusWithRawResponse);
			_logger.LogDebug("<<< Exiting RawRequest(requestFrame={RequestFrame})", requestFrame);
            return deviceStatusWithRawResponse;
        }

        public override DeviceStatusWithDateTime Reset(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering Reset(credentials=<redacted>)");
            AbortReceipt();
            FullPayment();
			_logger.LogDebug("<<< Exiting Reset(credentials=<redacted>)");
            return CheckStatus();
        }
    }
}
