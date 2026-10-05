namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
	using Microsoft.Extensions.Logging;

    public static class Helpers
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public static T[] Slice<T>(this T[] arr, uint indexFrom, uint indexTo)
        {
			_logger.LogInformation(">>> Entering Slice(arr={Arr},indexFrom={IndexFrom},indexTo={IndexTo})", arr, indexFrom, indexTo);
            if (indexFrom > indexTo)
            {
                throw new ArgumentOutOfRangeException("indexFrom is bigger than indexTo!");
            }

            uint length = indexTo - indexFrom;
            T[] result = new T[length];
            Array.Copy(arr, indexFrom, result, 0, length);

			_logger.LogDebug("<<< Exiting Slice(arr={Arr},indexFrom={IndexFrom},indexTo={IndexTo})", arr, indexFrom, indexTo);
            return result;
        }

        public static string WithMaxLength(this string text, int maxLength = 72)
        {
			_logger.LogInformation(">>> Entering WithMaxLength(text={Text},maxLength={MaxLength})", text, maxLength);
            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength);
        }

        public static IEnumerable<string> WrapAtLength(this string text, int maxLength)
        {
			_logger.LogInformation(">>> Entering WrapAtLength(text={Text},maxLength={MaxLength})", text, maxLength);
            if (text == null)
                throw new ArgumentNullException("text");
            if (maxLength < 1)
                throw new ArgumentException("'maxLength' must be greater than 0.");

            var rows = Math.Ceiling((double)text.Length / maxLength);
            if (rows < 2) return new string[] { text };

            var listOfStrings = new List<string>();

            for (int i = 0; i < rows; i++)
            {
                var index = i * maxLength;
                var length = Math.Min(maxLength, text.Length - index);
                listOfStrings.Add(text.Substring(index, length));
            }

			_logger.LogDebug("<<< Exiting WrapAtLength(text={Text},maxLength={MaxLength})", text, maxLength);
            return listOfStrings;
        }

        public static IDictionary<string, string> MergeWith(this IDictionary<string, string> options, IDictionary<string, string>? newOptions)
        {
            if (newOptions != null)
            {
                foreach (KeyValuePair<string, string> newOption in newOptions)
                {
                    options[newOption.Key] = newOption.Value;
                }
            }
            return options;
        }

        public static string ValueOrDefault(
            this IDictionary<string, string> options,
            string key,
            string defaultValue) =>
            options.TryGetValue(key, out var value) ? value : defaultValue;

        public static string IfNullOrEmpty(this string val, string fallback) =>
            string.IsNullOrEmpty(val) ? fallback : val;

        public static string IfNullOrEmpty(this string val, Func<string> fallback) =>
            string.IsNullOrEmpty(val) ? fallback() : val;

        public static string[] Split(this string str, int[] chunkSizes)
        {
            var listOfStrings = new List<string>();
            int ix = 0;
			_logger.LogInformation(">>> Entering Split(str={Str},chunkSizes={ChunkSizes})", str, chunkSizes);
            foreach (var chunkSize in chunkSizes)
            {
                if (ix + chunkSize > str.Length)
                {
                    break;
                }
                listOfStrings.Add(str.Substring(ix, chunkSize));
                ix += chunkSize;
            }
			_logger.LogDebug("<<< Exiting Split(str={Str},chunkSizes={ChunkSizes})", str, chunkSizes);
            return listOfStrings.ToArray();
        }
        public static int IntPow(this int a, int b)
        {
			_logger.LogInformation(">>> Entering IntPow(a={A},b={B})", a, b);
            int result = 1;
            for (int i = 0; i < b; i++)
                result *= a;
			_logger.LogDebug("<<< Exiting IntPow(a={A},b={B})", a, b);
            return result;
        }

        public static int ParseTimeout(this string s)
        {
			_logger.LogInformation(">>> Entering ParseTimeout(s={S})", s);
            int value = 0;
            var match = TimeoutRegex.Match(s);
            if (match.Success &&
                match.Groups.Count == 3 &&
                int.TryParse(match.Groups[1].Value, out value))
            {
                switch (match.Groups[2].Value.ToLowerInvariant())
                {
                    case "m":
                        value *= 60 * 1000;
                        break;
                    case "s":
                        value *= 1000;
                        break;
                    case "ms":
                    case "":
                        // keep the parsed value
                        break;
                    default:
                        // unknown sufix
                        value = 0;
                        break;
                }
            }
			_logger.LogDebug("<<< Exiting ParseTimeout(s={S})", s);
            return value;
        }

        private static readonly Regex TimeoutRegex = new Regex(@"^(\d+)\s*?([ms]*)\s*?$", RegexOptions.IgnoreCase);
    }
}
