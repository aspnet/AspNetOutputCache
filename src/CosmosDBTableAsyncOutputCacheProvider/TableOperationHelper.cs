// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider
{
    using Microsoft.Azure.CosmosDB.Table;
    using System;

    static class TableOperationHelper
    {
        public static TableOperation Retrieve(string rowkey)
        {
            return TableOperation.Retrieve<CacheEntity>(CacheEntity.GeneratePartitionKey(rowkey), CacheEntity.SanitizeKey(rowkey));
        }

        public static TableOperation Delete(string rowkey)
        {
            var entryToDel = new CacheEntity(rowkey, null, DateTime.UtcNow) {
                ETag = "*"
            };
            
            return TableOperation.Delete(entryToDel);
        }

        public static TableOperation InsertOrReplace(string key, object cacheItem, DateTime utcExpiry)
        {
            var cacheEntry = new CacheEntity(key, cacheItem, utcExpiry);
            return TableOperation.InsertOrReplace(cacheEntry);
        }
    }
}
