namespace ErpNet.FP.Core.Drivers.BgDatecs
{
    using System.Collections.Generic;
    using ErpNet.FP.Core.Configuration;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Fiscal printer using the ISL implementation of Datecs Bulgaria.
    /// </summary>
    /// <seealso cref="ErpNet.FP.Drivers.BgIslFiscalPrinter" />
    public partial class BgDatecsXIslFiscalPrinter : BgIslFiscalPrinter
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public BgDatecsXIslFiscalPrinter(
            IChannel channel, 
            ServiceOptions serviceOptions, 
            IDictionary<string, string>? options = null)
        : base(channel, serviceOptions, options) { }

        public override IDictionary<string, string>? GetDefaultOptions()
        {
            return new Dictionary<string, string>
            {
                ["Operator.ID"] = "1",
                ["Operator.Password"] = "0000",

                ["Administrator.ID"] = "20",
                ["Administrator.Password"] = "9999"
            };
        }

        public override DeviceStatus PrintDuplicate(Credentials credentials)
        {
			_logger.LogDebug(">>> Entering PrintDuplicate(credentials=<redacted>)");
            var (_, status) = Request(CommandPrintLastReceiptDuplicate);
			_logger.LogDebug("<<< Exiting PrintDuplicate(credentials=<redacted>)");
            return status;
        }

    }
}
