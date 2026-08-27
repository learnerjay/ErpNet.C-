namespace ErpNet.FP.Core.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
	using Microsoft.Extensions.Logging;

    public class ExpiringCache<TKey, TValue>
        where TKey : notnull
        where TValue : class
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        private readonly IDictionary<TKey, ExpiringCacheItem<TValue>>
            cacheDictionary = new ConcurrentDictionary<TKey, ExpiringCacheItem<TValue>>();

        public void Store(TKey key, TValue value, TimeSpan expiresAfter)
        {
			_logger.LogDebug(">>> Entering Store(key={Key},value={Value},expiresAfter={ExpiresAfter})", key, value, expiresAfter);
            cacheDictionary[key] = new ExpiringCacheItem<TValue>(value, expiresAfter);
			_logger.LogDebug("<<< Exiting Store(key={Key},value={Value},expiresAfter={ExpiresAfter})", key, value, expiresAfter);
        }

        public TValue? Get(TKey key)
        {
			_logger.LogDebug(">>> Entering Get(key={Key})", key);
            if (!cacheDictionary.ContainsKey(key)) return default;
            var cached = cacheDictionary[key];
            if (DateTimeOffset.Now - cached.Created >= cached.ExpiresAfter)
            {
                cacheDictionary.Remove(key);
			_logger.LogDebug("<<< Exiting Get(key={Key})", key);
                return default;
            }
            return cached.Value;
        }
    }

    public class ExpiringCacheItem<T>
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public ExpiringCacheItem(T value, TimeSpan expiresAfter)
        {
			_logger.LogDebug(">>> Entering ExpiringCacheItem(value={Value},expiresAfter={ExpiresAfter})", value, expiresAfter);
            Value = value;
			_logger.LogDebug("ExpiringCacheItem(value={Value},expiresAfter={ExpiresAfter}): Value → {Value1}", value, expiresAfter, Value);
            ExpiresAfter = expiresAfter;
        }
        public T Value { get; }
        internal DateTimeOffset Created { get; } = DateTimeOffset.Now;
        internal TimeSpan ExpiresAfter { get; }
    }

}
