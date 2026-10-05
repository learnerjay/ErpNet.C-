namespace ErpNet.FP.Server.Controllers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ErpNet.FP.Core;
    using ErpNet.FP.Core.Service;
    using ErpNet.FP.Core.Drivers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
	using Microsoft.Extensions.Logging;

    // PrintersController, example: //host/printers/[controller]
    [Route("[controller]")]
    [ApiController]
    public class PrintersController : ControllerBase
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        private readonly IServiceController context;

        public PrintersController(IServiceController context)
        {
			_logger.LogInformation(">>> Entering PrintersController(context={Context})", context);
            this.context = context;
			_logger.LogDebug("<<< Exiting PrintersController(context={Context})", context);
        }

        // GET /
        [HttpGet()]
        public ActionResult<Dictionary<string, DeviceInfo>> Printers()
        {
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            return context.PrintersInfo;
        }

        // GET {id}
        [HttpGet("{id}")]
        public ActionResult<DeviceInfo> Info(string id)
        {
			_logger.LogInformation(">>> Entering Info(id={Id})", id);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.PrintersInfo.TryGetValue(id, out DeviceInfo? deviceInfo))
            {
                return deviceInfo;
            }
			_logger.LogDebug("<<< Exiting Info(id={Id})", id);
            return NotFound();
        }

        // GET {id}/status
        [HttpGet("{id}/status")]
        public ActionResult<DeviceStatusWithDateTime> Status(string id)
        {
			_logger.LogInformation(">>> Entering Status(id={Id})", id);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                return printer.CheckStatus();
            }
			_logger.LogDebug("<<< Exiting Status(id={Id})", id);
            return NotFound();
        }

        // Get {id}/cash
        [HttpGet("{id}/cash")]
        public async Task<IActionResult> Cash(
            string id,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering Cash(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Cash,
                        Document = null,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting Cash(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // GET taskinfo
        [HttpGet("taskinfo")]
        public ActionResult<TaskInfoResult> TaskInfo([FromQuery]string id)
        {
			_logger.LogInformation(">>> Entering TaskInfo(id={Id})", id);
			_logger.LogDebug("<<< Exiting TaskInfo(id={Id})", id);
            return context.GetTaskInfo(id);
        }

        // POST {id}/rawrequest
        [HttpPost("{id}/rawrequest")]
        public async Task<IActionResult> RawRequest(
            string id,
            [FromBody] RequestFrame requestFrame,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering RawRequest(id={Id},requestFrame={RequestFrame},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, requestFrame, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.RawRequest,
                        Document = requestFrame,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting RawRequest(id={Id},requestFrame={RequestFrame},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, requestFrame, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/receipt
        [HttpPost("{id}/receipt")]
        public async Task<IActionResult> PrintReceipt(
            string id,
            [FromBody] Receipt receipt,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintReceipt(id={Id},receipt={Receipt},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, receipt, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Receipt,
                        Document = receipt,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintReceipt(id={Id},receipt={Receipt},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, receipt, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/reversalreceipt
        [HttpPost("{id}/reversalreceipt")]
        public async Task<IActionResult> PrintReversalReceipt(
            string id,
            [FromBody] ReversalReceipt reversalReceipt,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintReversalReceipt(id={Id},reversalReceipt={ReversalReceipt},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, reversalReceipt, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.ReversalReceipt,
                        Document = reversalReceipt,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(), 
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintReversalReceipt(id={Id},reversalReceipt={ReversalReceipt},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, reversalReceipt, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/invoice
        [HttpPost("{id}/invoice")]
        public async Task<IActionResult> PrintInvoice(
            string id,
            [FromBody] Invoice invoice,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintInvoice(id={Id},invoice={Invoice},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, invoice, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }

            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Invoice,
                        Document = invoice,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });

                return Ok(result);
            }

			_logger.LogDebug("<<< Exiting PrintInvoice(id={Id},invoice={Invoice},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, invoice, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/creditnote
        [HttpPost("{id}/creditnote")]
        public async Task<IActionResult> PrintCreditNote(
            string id,
            [FromBody] CreditNote creditNote,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintCreditNote(id={Id},creditNote={CreditNote},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, creditNote, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }

            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.CreditNote,
                        Document = creditNote,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });

                return Ok(result);
            }

			_logger.LogDebug("<<< Exiting PrintCreditNote(id={Id},creditNote={CreditNote},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, creditNote, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/withdraw
        [HttpPost("{id}/withdraw")]
        public async Task<IActionResult> PrintWithdraw(
            string id,
            [FromBody] TransferAmount withdraw,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintWithdraw(id={Id},withdraw={Withdraw},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, withdraw, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Withdraw,
                        Document = withdraw,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintWithdraw(id={Id},withdraw={Withdraw},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, withdraw, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/deposit
        [HttpPost("{id}/deposit")]
        public async Task<IActionResult> PrintDeposit(
            string id,
            [FromBody] TransferAmount deposit,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintDeposit(id={Id},deposit={Deposit},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, deposit, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Deposit,
                        Document = deposit,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintDeposit(id={Id},deposit={Deposit},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, deposit, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/datetime
        [HttpPost("{id}/datetime")]
        public async Task<IActionResult> SetDateTime(
            string id,
            [FromBody] CurrentDateTime datetime,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering SetDateTime(id={Id},datetime={Datetime},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, datetime, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.SetDateTime,
                        Document = datetime,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting SetDateTime(id={Id},datetime={Datetime},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, datetime, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/zreport
        [HttpPost("{id}/zreport")]
        public async Task<IActionResult> PrintZReport(
            string id,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintZReport(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.ZReport,
                        Document = null,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintZReport(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/xreport
        [HttpPost("{id}/xreport")]
        public async Task<IActionResult> PrintXReport(
            string id,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintXReport(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.XReport,
                        Document = null,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintXReport(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/periodreport
        [HttpPost("{id}/periodreport")]
        public async Task<IActionResult> PrintPeriodReport(
            string id,
            [FromBody] PeriodReport periodReport,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintPeriodReport(id={Id},periodReport={PeriodReport},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, periodReport, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }

            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.PeriodReport,
                        Document = periodReport,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });

                return Ok(result);
            }

			_logger.LogDebug("<<< Exiting PrintPeriodReport(id={Id},periodReport={PeriodReport},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, periodReport, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/duplicate
        [HttpPost("{id}/duplicate")]
        public async Task<IActionResult> PrintDuplicate(
            string id,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering PrintDuplicate(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Duplicate,
                        Document = null,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting PrintDuplicate(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            return NotFound();
        }

        // POST {id}/reset
        [HttpPost("{id}/reset")]
        public async Task<IActionResult> Reset(
            string id,
            [FromQuery] string? taskId,
            [FromQuery] string? timeout,
            [FromQuery] int asyncTimeout = PrintJob.DefaultTimeout)
        {
			_logger.LogInformation(">>> Entering Reset(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            if (!context.IsReady)
            {
                return StatusCode(StatusCodes.Status405MethodNotAllowed);
            }
            if (context.Printers.TryGetValue(id, out IFiscalPrinter? printer))
            {
                var result = await context.RunAsync(
                    new PrintJob
                    {
                        Printer = printer,
                        Action = PrintJobAction.Reset,
                        Document = null,
                        AsyncTimeout = asyncTimeout,
                        Timeout = timeout == null ? 0 : timeout.ParseTimeout(),
                        TaskId = taskId
                    });
                return Ok(result);
            }
			_logger.LogDebug("<<< Exiting Reset(id={Id},taskId={TaskId},timeout={Timeout},asyncTimeout={AsyncTimeout})", id, taskId, timeout, asyncTimeout);
            return NotFound();
        }
    }
}
