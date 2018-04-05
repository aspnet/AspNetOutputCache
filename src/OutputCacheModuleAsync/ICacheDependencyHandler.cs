// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System.Runtime.Caching;
    using System.Threading.Tasks;

    /// <summary>
    /// This interface is for the async outputCache providers to implement if they want to support Cache Dependencies 
    /// </summary>
    public interface ICacheDependencyHandler {
        /// <summary>
        /// Async Add method that takes cacheItemPolicy as parameter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="cacheItemPolicy"></param>
        /// <returns></returns>
        Task<object> AddAsync(string key, object entry, CacheItemPolicy cacheItemPolicy);

        /// <summary>
        /// Async Set method that takes cacheItemPolicy as parameter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="cacheItemPolicy"></param>
        /// <returns></returns>
        Task SetAsync(string key, object entry, CacheItemPolicy cacheItemPolicy);
    }
}
