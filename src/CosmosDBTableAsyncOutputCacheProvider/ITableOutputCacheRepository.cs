// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider {
    using System;
    using System.Threading.Tasks;

    interface ITableOutputCacheRepository {
        Task<object> AddAsync(string key, object entry, DateTime utcExpiry);

        Task<object> GetAsync(string key);

        Task RemoveAsync(string key);

        Task SetAsync(string key, object entry, DateTime utcExpiry);

        object Add(string key, object entry, DateTime utcExpiry);

        object Get(string key);

        void Remove(string key);

        void Set(string key, object entry, DateTime utcExpiry);
    }
}
