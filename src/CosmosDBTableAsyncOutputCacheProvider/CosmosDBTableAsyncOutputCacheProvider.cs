// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider
{
    using System;
    using System.Collections.Specialized;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web.Caching;
    using System.Web.Configuration;

    public class CosmosDBTableAsyncOutputCacheProvider : OutputCacheProviderAsync
    {
        private ITableOutputCacheRepository _tableRepo;

        public override void Initialize(string name, NameValueCollection config)
        {
            Initialize(name, config, WebConfigurationManager.AppSettings);
        }

        internal void Initialize(string name, NameValueCollection providerConfig, NameValueCollection appSettings)
        {
            _tableRepo = new CosmosDBTableOutputCacheRepository(providerConfig, appSettings);
            base.Initialize(name, providerConfig);
        }

        public override object Add(string key, object entry, DateTime utcExpiry)
        {
            return _tableRepo.Add(key, entry, utcExpiry);
        }

        public override Task<object> AddAsync(string key, object entry, DateTime utcExpiry)
        {
            return _tableRepo.AddAsync(key, entry, utcExpiry);
        }

        public override object Get(string key)
        {
            return _tableRepo.Get(key);
        }

        public override Task<object> GetAsync(string key)
        {
            return _tableRepo.GetAsync(key);
        }

        public override void Remove(string key)
        {
            _tableRepo.Remove(key);
        }

        public override Task RemoveAsync(string key)
        {
            return _tableRepo.RemoveAsync(key);
        }

        public override void Set(string key, object entry, DateTime utcExpiry)
        {
            _tableRepo.Set(key, entry, utcExpiry);
        }
        
        public override Task SetAsync(string key, object entry, DateTime utcExpiry)
        {
            return _tableRepo.SetAsync(key, entry, utcExpiry);
        }
    }
}
