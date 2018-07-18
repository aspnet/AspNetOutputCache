// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider {
    using System;
    using System.Collections.Specialized;
    using System.Threading.Tasks;
    using System.Web.Caching;
    using System.Web.Configuration;

    /// <summary>
    /// Async CosmosDB table OutputCache provider
    /// </summary>
    public class CosmosDBTableAsyncOutputCacheProvider : OutputCacheProviderAsync {
        private ITableOutputCacheRepository _tableRepo;

        /// <inheritdoc />
        public override void Initialize(string name, NameValueCollection config) {
            Initialize(name, config, new CosmosDBTableOutputCacheRepository(config, WebConfigurationManager.AppSettings));
        }

        internal void Initialize(string name, NameValueCollection providerConfig, ITableOutputCacheRepository _repo) {
            _tableRepo = _repo;
            base.Initialize(name, providerConfig);
        }

        /// <inheritdoc />
        public override object Add(string key, object entry, DateTime utcExpiry) {
            return _tableRepo.Add(key, entry, utcExpiry);
        }

        /// <inheritdoc />
        public override Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            return _tableRepo.AddAsync(key, entry, utcExpiry);
        }

        /// <inheritdoc />
        public override object Get(string key) {
            return _tableRepo.Get(key);
        }

        /// <inheritdoc />
        public override Task<object> GetAsync(string key) {
            return _tableRepo.GetAsync(key);
        }

        /// <inheritdoc />
        public override void Remove(string key) {
            _tableRepo.Remove(key);
        }

        /// <inheritdoc />
        public override Task RemoveAsync(string key) {
            return _tableRepo.RemoveAsync(key);
        }

        /// <inheritdoc />
        public override void Set(string key, object entry, DateTime utcExpiry) {
            _tableRepo.Set(key, entry, utcExpiry);
        }

        /// <inheritdoc />
        public override Task SetAsync(string key, object entry, DateTime utcExpiry) {
            return _tableRepo.SetAsync(key, entry, utcExpiry);
        }
    }
}
