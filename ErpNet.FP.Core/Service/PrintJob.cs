namespace ErpNet.FP.Core.Service
{
    using System;
	using Microsoft.Extensions.Logging;
    
    public enum PrintJobAction
    {
        None,
        Cash,
        RawRequest,
        Receipt,
        ReversalReceipt,
        Invoice,
        CreditNote,
        Withdraw,
        Deposit,
        XReport,
        ZReport,
        PeriodReport,
        SetDateTime,
        Duplicate,
        Reset
    }

    public delegate object Run(object document);

    public class PrintJob
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public const int DefaultTimeout = 29000; // 29 seconds

        public DateTime Enqueued = DateTime.Now;
        public DateTime? Started = null;
        public DateTime? Finished = null;
        public DateTime DeadLine = DateTime.MaxValue;

        public PrintJobAction Action = PrintJobAction.None;
        public IFiscalPrinter? Printer;
        public TaskStatus TaskStatus = TaskStatus.Enqueued;  // On creation status must be "Enqueued"!  "Unknown" is for task NOT FOUND in queue.
        public object? Document;
        public object? Result;
        public string? TaskId;
        public int AsyncTimeout = DefaultTimeout;

        public int Timeout { 
            get => timeout; 
            set {
                timeout = value;
                DeadLine = timeout <= 0 ? DateTime.MaxValue : Enqueued.AddMilliseconds(timeout);
            } 
        }

        public void Run()
        {
			_logger.LogDebug(">>> Entering Run()");
            if (Printer == null) return;
            
            Started = DateTime.Now;
			_logger.LogDebug("Run(): Started → {Started}", Started);

            if (DeadLine <= Started)
            {                
                Finished = DateTime.Now;
                TaskStatus = TaskStatus.Finished;
                var deviceStatus = new DeviceStatus();
				_logger.LogDebug("Run(): deviceStatus → {DeviceStatus}", deviceStatus);
				_logger.LogDebug("Run(): Finished → {Finished}", Finished);
				_logger.LogDebug("Run(): TaskStatus → {TaskStatus}", TaskStatus);
                deviceStatus.AddError("E999", "User timeout occured while sending the request");
                Result = deviceStatus;
				_logger.LogDebug("Run(): Result → {Result}", Result);
                return;
            }

            TaskStatus = TaskStatus.Running;

            Printer.SetDeadLine(DeadLine);
            try
            {
                switch (Action)
                {
                    case PrintJobAction.Cash:
                        Result = Printer.Cash((Credentials)(Document ?? new Credentials()));
                        break;
                    case PrintJobAction.RawRequest:
                        if (Document != null)
                        {
                            Result = Printer.RawRequest((RequestFrame)Document);
                        }
                        break;
                    case PrintJobAction.Receipt:
                        if (Document != null)
                        {
                            var receipt = (Receipt)Document;
							_logger.LogDebug("Run(): receipt → {Receipt}", receipt);
                            var validateStatus = Printer.ValidateReceipt(receipt);
							_logger.LogDebug("Run(): validateStatus → {ValidateStatus}", validateStatus);
                            if (validateStatus.Ok)
                            {
                                var (info, status) = Printer.PrintReceipt(receipt);
                                Result = info.ToDeviceStatus(status);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        }
                        break;
                    case PrintJobAction.ReversalReceipt:
                        if (Document != null)
                        {
                            var reversalReceipt = (ReversalReceipt)Document;
							_logger.LogDebug("Run(): reversalReceipt → {ReversalReceipt}", reversalReceipt);
                            var validateStatus = Printer.ValidateReversalReceipt(reversalReceipt);
                            if (validateStatus.Ok)
                            {
                                var (info, status) = Printer.PrintReversalReceipt(reversalReceipt);
                                Result = info.ToDeviceStatus(status);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        };
                        break;
                    case PrintJobAction.Invoice:
                        if (Document != null)
                        {
                            var invoice = (Invoice)Document;
							_logger.LogDebug("Run(): invoice → {Invoice}", invoice);
                            var validateStatus = Printer.ValidateInvoice(invoice);
                            if (validateStatus.Ok)
                            {
                                var (info, status) = Printer.PrintInvoice(invoice);
                                Result = info.ToDeviceStatus(status);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        }
                        break;
                    case PrintJobAction.CreditNote:
                        if (Document != null)
                        {
                            var creditNote = (CreditNote)Document;
							_logger.LogDebug("Run(): creditNote → {CreditNote}", creditNote);
                            var validateStatus = Printer.ValidateCreditNote(creditNote);
                            if (validateStatus.Ok)
                            {
                                var (info, status) = Printer.PrintCreditNote(creditNote);
                                Result = info.ToDeviceStatus(status);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        }
                        break;
                    case PrintJobAction.Withdraw:
                        if (Document != null)
                        {
                            var transferAmount = (TransferAmount)Document;
							_logger.LogDebug("Run(): transferAmount → {TransferAmount}", transferAmount);
                            var validateStatus = Printer.ValidateTransferAmount(transferAmount);
                            if (validateStatus.Ok)
                            {
                                Result = Printer.PrintMoneyWithdraw(transferAmount);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        }
                        break;
                    case PrintJobAction.Deposit:
                        if (Document != null)
                        {
                            var transferAmount = (TransferAmount)Document;
                            var validateStatus = Printer.ValidateTransferAmount(transferAmount);
                            if (validateStatus.Ok)
                            {
                                Result = Printer.PrintMoneyDeposit((TransferAmount)Document);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        }
                        break;
                    case PrintJobAction.XReport:
                        Result = Printer.PrintXReport((Credentials)(Document ?? new Credentials()));
                        break;
                    case PrintJobAction.ZReport:
                        Result = Printer.PrintZReport((Credentials)(Document ?? new Credentials()));
                        break;
                    case PrintJobAction.PeriodReport:
                        if (Document != null)
                        {
                            var periodReport = (PeriodReport)Document;
							_logger.LogDebug("Run(): periodReport → {PeriodReport}", periodReport);
                            var validateStatus = Printer.ValidatePeriodReport(periodReport);
                            if (validateStatus.Ok)
                            {
                                Result = Printer.PrintPeriodReport(periodReport);
                            }
                            else
                            {
                                Result = validateStatus;
                            }
                        }
                        break;
                    case PrintJobAction.SetDateTime:
                        if (Document != null)
                        {
                            var dateTimeDocument = (CurrentDateTime)Document;
							_logger.LogDebug("Run(): dateTimeDocument → {DateTimeDocument}", dateTimeDocument);
                            if (dateTimeDocument.DeviceDateTime == System.DateTime.MinValue)
                            {
                                dateTimeDocument.DeviceDateTime = DateTime.Now;
								_logger.LogDebug("Run(): dateTimeDocument.DeviceDateTime → {Arg0}", dateTimeDocument.DeviceDateTime);
                            }
                            Result = Printer.SetDateTime(dateTimeDocument);
                        };
                        break;
                    case PrintJobAction.Duplicate:
                        Result = Printer.PrintDuplicate((Credentials)(Document ?? new Credentials()));
                        break;
                    case PrintJobAction.Reset:
                        Result = Printer.Reset((Credentials)(Document ?? new Credentials()));
                        break;
                    default:
                        break;
                }
            }
            finally
            {
                Finished = DateTime.Now;
                TaskStatus = TaskStatus.Finished;
                Printer.SetDeadLine(DateTime.MaxValue);
			_logger.LogDebug("<<< Exiting Run()");
            }
        }

        private int timeout = 0;
    }
}
