namespace ErpNet.FP.Core
{
    using System;
	using Microsoft.Extensions.Logging;

    public class StandardizedStatusMessageException : Exception
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public string Code = string.Empty;
        public StatusMessageType Type = StatusMessageType.Error;

        public StandardizedStatusMessageException() { }
        
        public StandardizedStatusMessageException(string message) : base(message) { }

        public StandardizedStatusMessageException(string message, string code) : base(message)
        {
			_logger.LogInformation(">>> Entering StandardizedStatusMessageException(message={Message},code={Code})", message, code);
            _logger.LogDebug("<<< Exiting StandardizedStatusMessageException(message={Message})", message);
            Code = code;
        }

        public StandardizedStatusMessageException(string message, string code, StatusMessageType type) : base(message)
        {
            Code = code;
            Type = type;
        }
        
        public StandardizedStatusMessageException(string message, Exception inner) : base(message, inner) { }
    }
}
