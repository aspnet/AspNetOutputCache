namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    using OutputCache;
    using System;
    using System.Collections.Specialized;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web.Caching;

    /// <summary>
    /// Async OutputCache Provider using SQL as storage.
    /// </summary>
    public class SQLAsyncOutputCacheProvider : OutputCacheProviderAsync, ICacheDependencyHandler {

        static SQLHelper sqlUtilityHelper;
        public override void Initialize(string name, NameValueCollection config) {
            if (config == null) {
                throw new ArgumentNullException("config");
            }
            if (String.IsNullOrEmpty(name)) {
                name = "SqlAsyncOutputCacheProvider";
            }
            base.Initialize(name, config);
            sqlUtilityHelper = new SQLHelper(config);
        }

        #region async methods
        /// <summary>
        /// Asynchronously inserts the specified entry into the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override async Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            return await sqlUtilityHelper.AddAsync(key, entry, utcExpiry);
        }

        /// <summary>
        /// Asynchronously returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override async Task<object> GetAsync(string key) {
            return await sqlUtilityHelper.GetAsync(key);
        }

        /// <summary>
        /// Asynchronously Inserts the specified entry into the output cache, overwriting the entry if it is already cached.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override async Task SetAsync(string key, object entry, DateTime utcExpiry) {
            await sqlUtilityHelper.SetAsync(key, entry, utcExpiry);
        }

        /// <summary>
        /// Asynchronously removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override async Task RemoveAsync(string key) {
            await sqlUtilityHelper.RemoveAsync(key);
        }
        #endregion

        #region sync methods
        /// <summary>
        /// Returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override object Get(string key) {
            return GetAsync(key);
        }

        /// <summary>
        /// Inserts the specified entry into the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override object Add(string key, object entry, DateTime utcExpiry) {
            return AddAsync(key, entry, utcExpiry);
        }

        /// <summary>
        /// Inserts the specified entry into the output cache, overwriting the entry if it is already cached
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        public override void Set(string key, object entry, DateTime utcExpiry) {
            SetAsync(key, entry, utcExpiry);
        }

        /// <summary>
        /// Removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key"></param>
        public override void Remove(string key) {
            RemoveAsync(key);
        }
        #endregion

        #region Methods support CacheItemPolicy

        public async Task<object> AddAsync(string key, object entry, CacheItemPolicy cacheItemPolicy) {
            //TODO: Decide what to work on the monitors
            return await sqlUtilityHelper.AddAsync(key, entry, cacheItemPolicy.AbsoluteExpiration.DateTime);
        }

        public async Task SetAsync(string key, object entry, CacheItemPolicy cacheItemPolicy) {
            //TODO: Decide what to work on the monitors
            await sqlUtilityHelper.SetAsync(key, entry, cacheItemPolicy.AbsoluteExpiration.DateTime);
        }
    }
    #endregion
}

