// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    using OutputCache;
    using System;
    using System.Collections.Specialized;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web.Caching;

    /// <summary>
    /// Async OutputCache Provider using SQL server as storage.
    /// </summary>
    public class SQLAsyncOutputCacheProvider : OutputCacheProviderAsync, ICacheDependencyHandler {

        #region Private Fields
        static ISqlOutputCacheRepository sqlRepository;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the SQL Async OutputCache Provider
        /// </summary>
        /// <param name="name"></param>
        /// <param name="config"></param>
        public override void Initialize(string name, NameValueCollection config) {
            if (config == null) {
                throw new ArgumentNullException("config");
            }
            if (String.IsNullOrEmpty(name)) {
                name = "SqlAsyncOutputCacheProvider";
            }

            Initialize(name, config, new SqlOutputCacheRepository(config));
        }

        internal void Initialize(string name, NameValueCollection config, ISqlOutputCacheRepository repository) {
            sqlRepository = repository;

            base.Initialize(name, config);
        }
        #endregion

        #region Public Async Methods
        /// <summary>
        /// Asynchronously inserts the specified entry into the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override async Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            return await sqlRepository.AddAsync(key, entry, utcExpiry);
        }

        /// <summary>
        /// Asynchronously returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override async Task<object> GetAsync(string key) {
            return await sqlRepository.GetAsync(key);
        }

        /// <summary>
        /// Asynchronously Inserts the specified entry into the output cache, overwriting the entry if it is already cached.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override async Task SetAsync(string key, object entry, DateTime utcExpiry) {
            await sqlRepository.SetAsync(key, entry, utcExpiry);
        }

        /// <summary>
        /// Asynchronously removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override async Task RemoveAsync(string key) {
            await sqlRepository.RemoveAsync(key);
        }
        #endregion

        #region Public Sync methods
        /// <summary>
        /// Returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override object Get(string key) {
            return sqlRepository.Get(key);
        }

        /// <summary>
        /// Inserts the specified entry into the output cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override object Add(string key, object entry, DateTime utcExpiry) {
            return sqlRepository.Add(key, entry, utcExpiry);
        }

        /// <summary>
        /// Inserts the specified entry into the output cache, overwriting the entry if it is already cached
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        public override void Set(string key, object entry, DateTime utcExpiry) {
            sqlRepository.Set(key, entry, utcExpiry);
        }

        /// <summary>
        /// Removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key"></param>
        public override void Remove(string key) {
            sqlRepository.Remove(key);
        }
        #endregion

        #region Public Async Methods that support CacheItemPolicy as Parameter
        /// <summary>
        /// Async Add method that supports CacheItemPolicy as Parameter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="cacheItemPolicy"></param>
        /// <returns></returns>
        public async Task<object> AddAsync(string key, object entry, CacheItemPolicy cacheItemPolicy) {
            return await sqlRepository.AddAsync(key, entry, cacheItemPolicy.AbsoluteExpiration.DateTime);
        }

        /// <summary>
        /// Async Set method that supports CacheItemPolicy
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="cacheItemPolicy"></param>
        /// <returns></returns>
        public async Task SetAsync(string key, object entry, CacheItemPolicy cacheItemPolicy) {
            await sqlRepository.SetAsync(key, entry, cacheItemPolicy.AbsoluteExpiration.DateTime);
        }
    }
    #endregion
}

