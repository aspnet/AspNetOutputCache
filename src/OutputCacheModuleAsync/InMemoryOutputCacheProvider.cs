namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web.Caching;
    using Resource;

    class InMemoryOutputCacheProvider : OutputCacheProviderAsync {
        private readonly MemoryCache _cache = new MemoryCache(SR.Microsoft_AspNet_OutputCache_Default_InMemory_Provider);

        public override Task<object> GetAsync(string key) {
            return Task.FromResult(Get(key)); 
        }

        public override Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            return Task.FromResult(Add(key,entry,utcExpiry));
        }

        public override Task SetAsync(string key, object entry, DateTime utcExpiry) {
            Set(key, entry, utcExpiry);
            return Task.CompletedTask;
        }

        public override Task RemoveAsync(string key) { 
            Remove(key);
            return Task.CompletedTask;
        }

        public override object Get(string key) {
            return _cache.Get(key);
        }

        public override object Add(string key, object entry, DateTime utcExpiry) {
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;           
            return _cache.Add(key, entry, expiration) ? entry : null;
        }

        public void Add(string depKey, DependencyCacheEntryWrapper dcew, DateTimeOffset dateTimeOffsetValue) {
            _cache.Add(depKey, dcew, dateTimeOffsetValue);
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
