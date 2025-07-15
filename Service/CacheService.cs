using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions;
using Microsoft.Extensions.Caching.Memory;

namespace NTH.Service
{
    public class CacheService
    {
        private readonly IMemoryCache _cache;
        private static readonly List<string> _cacheKey = [];

        public CacheService(IMemoryCache cache) {
            _cache = cache;
        }

        public void AddToCache(string key, object? data, TimeSpan? expiration = null)
        {
            if (!_cacheKey.Contains(key))
                _cacheKey.Add(key);

            _cache.Set(key, data, new MemoryCacheEntryOptions()
            {
                SlidingExpiration = expiration.HasValue ? expiration : TimeSpan.FromDays(1)
            });
        }

        public T? GetFixedData<T>(string key)
        {
            if (!_cache.TryGetValue(key, out T? cachedData))
            {
                return default;
            }

            return cachedData;
        }


        public void RemoveItemInCache(string key)
        {
            _cache.Remove(key);
            _cacheKey.Remove(key);
        }
    }
}
