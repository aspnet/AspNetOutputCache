using Microsoft.Azure.CosmosDB.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider
{
    static class TableOperationWrapper
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
