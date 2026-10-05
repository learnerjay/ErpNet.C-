namespace ErpNet.FP.Core
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
	using Microsoft.Extensions.Logging;

    public enum StatusMessageType
    {
        [EnumMember(Value = "")]
        Unknown,
        [EnumMember(Value = "reserved")]
        Reserved,
        [EnumMember(Value = "info")]
        Info,
        [EnumMember(Value = "warning")]
        Warning,
        [EnumMember(Value = "error")]
        Error
    }

    public class StatusMessage
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        [JsonConverter(typeof(StringEnumConverter))]
        public StatusMessageType Type { get; set; } = StatusMessageType.Unknown;

        /* Error and Warning Codes are strings with length of 5 characters.
        First 3 characters are the type of the error, i.e., ERR, WRN.
        Next 2 characters are digits, representing the ID of the error or warning. */
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Code { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class DeviceStatus
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public bool Ok { get; protected set; } = true;
        public IList<StatusMessage> Messages { get; protected set; } = new List<StatusMessage>();

        public void AddMessage(StatusMessage statusMessage)
        {
			_logger.LogInformation(">>> Entering AddMessage(statusMessage={StatusMessage})", statusMessage);
            if (statusMessage.Type == StatusMessageType.Unknown)
            {
                throw new System.ArgumentException("status message type cannot be unknown");
            }
            if (statusMessage.Type == StatusMessageType.Reserved)
            {
                // Ignore reserved messages
                return;
            }
            if (statusMessage.Type == StatusMessageType.Error)
            {
                Ok = false;
            }
            if (String.IsNullOrEmpty(statusMessage.Code))
            {
                statusMessage.Code = null;
            }
            Messages.Add(statusMessage);
			_logger.LogDebug("<<< Exiting AddMessage(statusMessage={StatusMessage})", statusMessage);
        }

        public void AddInfo(string text)
        {
			_logger.LogInformation(">>> Entering AddInfo(text={Text})", text);
            AddMessage(new StatusMessage
            {
                Type = StatusMessageType.Info,
                Text = text
            });
			_logger.LogDebug("<<< Exiting AddInfo(text={Text})", text);
        }

        public void AddError(string code, string text)
        {
			_logger.LogInformation(">>> Entering AddError(code={Code},text={Text})", code, text);
            AddMessage(new StatusMessage
            {
                Type = StatusMessageType.Error,
                Code = code,
                Text = text
            });
			_logger.LogDebug("<<< Exiting AddError(code={Code},text={Text})", code, text);
        }

        public void AddWarning(string code, string text)
        {
			_logger.LogInformation(">>> Entering AddWarning(code={Code},text={Text})", code, text);
            AddMessage(new StatusMessage
            {
                Type = StatusMessageType.Warning,
                Code = code,
                Text = text
            });
			_logger.LogDebug("<<< Exiting AddWarning(code={Code},text={Text})", code, text);
        }
    }

    public class DeviceStatusWithDateTime : DeviceStatus
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public System.DateTime DeviceDateTime { get; set; }

        public DeviceStatusWithDateTime(DeviceStatus status) : base()
        {
			_logger.LogInformation(">>> Entering DeviceStatusWithDateTime(status={Status})", status);
            Ok = status.Ok;
            Messages = status.Messages;
			_logger.LogDebug("<<< Exiting DeviceStatusWithDateTime(status={Status})", status);
        }
    }
    public class DeviceStatusWithRawResponse : DeviceStatus
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public string RawResponse { get; set; } = string.Empty;

        public DeviceStatusWithRawResponse(DeviceStatus status) : base()
        {
			_logger.LogInformation(">>> Entering DeviceStatusWithRawResponse(status={Status})", status);
            Ok = status.Ok;
            Messages = status.Messages;
			_logger.LogDebug("<<< Exiting DeviceStatusWithRawResponse(status={Status})", status);
        }
    }

    public class DeviceStatusWithCashAmount : DeviceStatus
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public decimal Amount { get; set; }

        public DeviceStatusWithCashAmount(DeviceStatus status) : base()
        {
			_logger.LogInformation(">>> Entering DeviceStatusWithCashAmount(status={Status})", status);
            Ok = status.Ok;
            Messages = status.Messages;
			_logger.LogDebug("<<< Exiting DeviceStatusWithCashAmount(status={Status})", status);
        }
    }

    public class DeviceStatusWithReceiptInfo : DeviceStatus
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        /// <summary>
        /// The receipt number.
        /// </summary>
        public string ReceiptNumber = string.Empty;
        /// <summary>
        /// The receipt date and time.
        /// </summary>
        public System.DateTime ReceiptDateTime;
        /// <summary>
        /// The receipt amount.
        /// </summary>
        public decimal ReceiptAmount = 0m;
        /// <summary>
        /// The fiscal memory number.
        /// </summary>
        public string FiscalMemorySerialNumber = string.Empty;

        public DeviceStatusWithReceiptInfo(DeviceStatus status, ReceiptInfo info) : base()
        {
			_logger.LogInformation(">>> Entering DeviceStatusWithReceiptInfo(status={Status},info={Info})", status, info);
            Ok = status.Ok;
            Messages = status.Messages;
            ReceiptNumber = info.ReceiptNumber;
            ReceiptDateTime = info.ReceiptDateTime;
            ReceiptAmount = info.ReceiptAmount;
            FiscalMemorySerialNumber = info.FiscalMemorySerialNumber;
			_logger.LogDebug("<<< Exiting DeviceStatusWithReceiptInfo(status={Status},info={Info})", status, info);
        }

    }

    public class DeviceStatusWithInvoiceInfo : DeviceStatusWithReceiptInfo
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        /// <summary>
        /// The invoice / credit note number.
        /// </summary>
        public string InvoiceNumber = string.Empty;

        public DeviceStatusWithInvoiceInfo(DeviceStatus status, InvoiceInfo info) : base(status, info)
        {
			_logger.LogInformation(">>> Entering DeviceStatusWithInvoiceInfo(status={Status},info={Info})", status, info);
            InvoiceNumber = info.InvoiceNumber;
			_logger.LogDebug("<<< Exiting DeviceStatusWithInvoiceInfo(status={Status},info={Info})", status, info);
        }
    }

}
