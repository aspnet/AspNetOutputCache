using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    interface ISqlOutputCacheRepository {
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
