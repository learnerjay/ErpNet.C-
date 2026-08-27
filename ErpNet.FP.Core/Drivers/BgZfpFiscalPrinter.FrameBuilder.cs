namespace ErpNet.FP.Core.Drivers
{
    using System.IO;
    using System.Text;
	using Microsoft.Extensions.Logging;

    public partial class BgZfpFiscalPrinter
    {
        /// <summary>
        /// Builds a binary protocol payload that mixes encoded text segments with raw bytes.
        /// Avoids the CP1251 round-trip problem for byte values in the U+0080–U+009F range.
        /// </summary>
        protected sealed class FrameBuilder
        {
        	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            private readonly MemoryStream _buffer = new();
            private readonly Encoding _encoding;

            /// <summary>
            /// Creates a new builder that encodes text segments using the
            /// given encoding (typically the printer's CP1251 encoding).
            /// </summary>
            public FrameBuilder(Encoding encoding)
            {
				_logger.LogDebug(">>> Entering FrameBuilder(encoding={Encoding})", encoding);
                _encoding = encoding;
				_logger.LogDebug("FrameBuilder(encoding={Encoding}): _encoding → {_encoding}", encoding, _encoding);
            }

            /// <summary>
            /// Appends a text segment, encoded via the configured encoding.
            /// </summary>
            public FrameBuilder Append(string text)
            {
				_logger.LogDebug(">>> Entering Append(text={Text})", text);
                var bytes = _encoding.GetBytes(text);
                _buffer.Write(bytes, 0, bytes.Length);
				_logger.LogDebug("Append(text={Text}): bytes → {Bytes}", text, bytes);

				_logger.LogDebug("<<< Exiting Append(text={Text})", text);
				_logger.LogDebug("<<< Exiting Append(c={C})", c);
				_logger.LogDebug("<<< Exiting Append(b={B})", b);
                return this;
            }

            /// <summary>
            /// Appends a single character, encoded via the configured encoding.
            /// </summary>
            public FrameBuilder Append(char c)
            {
                var bytes = _encoding.GetBytes([c]);
                _buffer.Write(bytes, 0, bytes.Length);

                return this;
            }

            /// <summary>
            /// Appends a single raw byte, bypassing the encoding entirely.
            /// </summary>
            public FrameBuilder Append(byte b)
            {
                _buffer.WriteByte(b);

                return this;
            }

            /// <summary>
            /// Returns the accumulated payload as a new byte array.
            /// </summary>
            public byte[] ToArray() => _buffer.ToArray();
        }
    }
}
