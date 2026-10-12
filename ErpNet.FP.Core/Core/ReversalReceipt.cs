namespace ErpNet.FP.Core
{
    using System.Linq;
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// Reversal Reason
    /// </summary>
    public enum ReversalReason
    {
        [EnumMember(Value = "operator-error")]
        OperatorError = 1,
        [EnumMember(Value = "refund")]
        Refund = 2,
        [EnumMember(Value = "tax-base-reduction")]
        TaxBaseReduction = 3,
        [EnumMember(Value = "taxbase-reduction")]
        OldCompatibleTaxBaseReduction = 3   // must be same as TaxBaseReduction
    }

    /// <summary>
    /// Represents one Receipt, which can be printed on a fiscal printer.
    /// </summary>
    public class ReversalReceipt : Receipt
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public string ReceiptNumber { get; set; } = string.Empty;
        public System.DateTime ReceiptDateTime { get; set; }
        public string FiscalMemorySerialNumber { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public ReversalReason Reason { get; set; } = ReversalReason.OperatorError;

        public ReversalReceipt CloneReceipt(Receipt receipt)
        {
			_logger.LogInformation(">>> Entering CloneReceipt(receipt={Receipt})", receipt);
            if (receipt.Items != null)
            {
                Items = receipt.Items.ToList();
            }
			_logger.LogDebug("CloneReceipt(receipt={Receipt}): Items → {Items}", receipt, Items);
            if (receipt.Payments != null)
            {
                Payments = receipt.Payments.ToList();
            }
			_logger.LogDebug("CloneReceipt(receipt={Receipt}): Payments → {Payments}", receipt, Payments);
            UniqueSaleNumber = receipt.UniqueSaleNumber;
			_logger.LogDebug("CloneReceipt(receipt={Receipt}): UniqueSaleNumber → {UniqueSaleNumber}", receipt, UniqueSaleNumber);
			_logger.LogDebug("<<< Exiting CloneReceipt(receipt={Receipt})", receipt);
            return this;
        }
    }
}