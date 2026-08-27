namespace ErpNet.FP.Core.Drivers.BgSis
{
    using ErpNet.FP.Core.Transports;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// <see cref="HttpTransport"/> specialized for the SIS Fiscal Module's JSON-RPC endpoint.
    /// </summary>
    public class BgSisJsonHttpTransport : HttpTransport
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public BgSisJsonHttpTransport()
            : base(defaultPath: "/jsonrpc", contentType: "application/json; charset=utf-8")
        {
			_logger.LogDebug("<<< Exiting BgSisJsonHttpTransport()");
			_logger.LogDebug(">>> Entering BgSisJsonHttpTransport()");
        }
    }
}
