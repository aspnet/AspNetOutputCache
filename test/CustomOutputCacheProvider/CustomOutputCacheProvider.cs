namespace Microsoft.AspNet.OutputCache.CustomOutputCacheProvider {
    using System;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web.Caching;

    /// <summary>
    /// This is just a proof of concept Async OutputCache Provider. It is used for testing purpose.
    /// </summary>
    public class CustomOutputCacheProvider : OutputCacheProviderAsync {
        private readonly static MemoryCache _cache = new MemoryCache("CustomOutputCacheProvider");
        
        /// <summary>
        /// Asynchronously inserts the specified entry into the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            //TODO:
            //Replace with your own async data insertion mechanism.
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            return Task.FromResult(_cache.AddOrGetExisting(key, entry, expiration));
        }

        /// <summary>
        /// Asynchronously returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override Task<object> GetAsync(string key) {
            //TODO:
            //Replace with your own aysnc data retrieve mechanism.
            return Task.FromResult(_cache.Get(key));
        }

        /// <summary>
        /// Asynchronously Inserts the specified entry into the output cache, overwriting the entry if it is already cached.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override Task SetAsync(string key, object entry, DateTime utcExpiry) {
            //TODO:
            //Replace with your own async insertion/overwriting mechanism.
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            _cache.Set(key, entry, expiration);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Asynchronously removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override Task RemoveAsync(string key) {
            //TODO:
            //Replace with your own async data removal mechanism.
            _cache.Remove(key);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override object Get(string key) {
            //TODO:
            //Replace with your own data retrieve mechanism.
            return _cache.Get(key);
        }

        /// <summary>
        /// Inserts the specified entry into the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override object Add(string key, object entry, DateTime utcExpiry) {
            //TODO:
            //Replace with your own data insertion mechanism.
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            return _cache.AddOrGetExisting(key, entry, expiration);
        }

        /// <summary>
        /// Inserts the specified entry into the output cache, overwriting the entry if it is already cached
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        public override void Set(string key, object entry, DateTime utcExpiry) {
            //TODO:
            //Replace with your own insertion/overwriting mechanism.
            DateTimeOffset expiration = (utcExpiry == Cache.NoAbsoluteExpiration) ? ObjectCache.InfiniteAbsoluteExpiration : utcExpiry;
            _cache.Set(key, entry, expiration);
        }

        /// <summary>
        /// Removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key"></param>
        public override void Remove(string key) {
            //TODO:
            //Replace with your own data removal mechanism.
            _cache.Remove(key);
        }
    }
}

