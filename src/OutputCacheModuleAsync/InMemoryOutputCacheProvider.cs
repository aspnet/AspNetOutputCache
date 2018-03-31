namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web.Caching;

    class InMemoryOutputCacheProvider : OutputCacheProviderAsync, ICacheDependencyHandler {
        private static ObjectCache _cache = new MemoryCache("InMemoryOutputCacheProvider");

        internal static ObjectCache InternalCache
        {
            get { return _cache; }
            set { _cache = value; }
        }

        public override Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            return Task.FromResult(_cache.AddOrGetExisting(key, entry, expiration));
        }

        Task<object> ICacheDependencyHandler.AddAsync(string key, object entry, CacheItemPolicy cacheItemPolicy) {
            return Task.FromResult(_cache.AddOrGetExisting(key, entry, cacheItemPolicy));
        }

        Task ICacheDependencyHandler.SetAsync(string key, object entry, CacheItemPolicy cacheItemPolicy) {
            _cache.Set(key, entry, cacheItemPolicy);
            return Task.CompletedTask;
        }

        public override Task<object> GetAsync(string key) {
            return Task.FromResult(_cache.Get(key));
        }

        public override Task SetAsync(string key, object entry, DateTime utcExpiry) {
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            _cache.Set(key, entry, expiration);
            return Task.CompletedTask;
        }

        public override Task RemoveAsync(string key) {
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        public override object Get(string key) {
            return _cache.Get(key);
        }

        public override object Add(string key, object entry, DateTime utcExpiry) {
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            return _cache.AddOrGetExisting(key, entry, expiration);
        }

        public override void Set(string key, object entry, DateTime utcExpiry) {
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            _cache.Set(key, entry, expiration);
        }

        public override void Remove(string key) {
            _cache.Remove(key);
        }
    }
}
