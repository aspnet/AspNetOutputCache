namespace Microsoft.AspNet.OutputCache.CustomOutputCacheProvider {
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Web.Caching;

    class CustomOutputCacheItem {
        public object Obj;
        public DateTime UtcExpiry;
        public CustomOutputCacheItem(object entry, DateTime utcExpiryIn) {
            Obj = entry;
            UtcExpiry = utcExpiryIn;
        }
    }

    /// <summary>
    /// This is just a proof of concept Async OutputCache Provider. It is used for testing purpose.
    /// </summary>
    public class CustomOutputCacheProvider : OutputCacheProviderAsync {

        private readonly Dictionary<string, CustomOutputCacheItem> _dict;

        /// <summary>
        /// Async OutputCache Provider
        /// </summary>
        public CustomOutputCacheProvider() {
            _dict = new Dictionary<string, CustomOutputCacheItem>();
        }

        private static async Task FooAsync() {
            await Task.Delay(1);
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override async Task<object> GetAsync(string key) {
            await FooAsync();
            if (!_dict.ContainsKey(key)) {
                return null;
            }
            if (_dict[key].UtcExpiry > DateTime.Now.ToUniversalTime()) {
                return _dict[key].Obj;
            }
            await RemoveAsync(key);
            return null;
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override async Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            await FooAsync();
            if (_dict.ContainsKey(key) && _dict[key].UtcExpiry > DateTime.Now.ToUniversalTime()) {
                return _dict[key].Obj;
            }
            if (!_dict.ContainsKey(key)) {
                _dict.Add(key, new CustomOutputCacheItem(entry, utcExpiry));
            }
            else {
                _dict[key] = new CustomOutputCacheItem(entry, utcExpiry);
            }
            return entry;
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override async Task SetAsync(string key, object entry, DateTime utcExpiry) {
            await FooAsync();
            if (_dict.ContainsKey(key)) {
                _dict[key] = new CustomOutputCacheItem(entry, utcExpiry);
            }
            else {
                _dict.Add(key, new CustomOutputCacheItem(entry, utcExpiry));
            }
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override async Task RemoveAsync(string key) {
            await FooAsync();
            _dict.Remove(key);
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override object Get(string key) {
            if (!_dict.ContainsKey(key)) {
                return null;
            }
            if (_dict[key].UtcExpiry > DateTime.Now.ToUniversalTime()) {
                return _dict[key].Obj;
            }
            Remove(key);
            return null;
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        /// <returns></returns>
        public override object Add(string key, object entry, DateTime utcExpiry) {
            if (_dict.ContainsKey(key) && _dict[key].UtcExpiry > DateTime.Now.ToUniversalTime()) {
                return _dict[key].Obj;
            }
            if (!_dict.ContainsKey(key)) {
                _dict.Add(key, new CustomOutputCacheItem(entry, utcExpiry));
            }
            else {
                _dict[key] = new CustomOutputCacheItem(entry, utcExpiry);
            }
            return entry;
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        /// <param name="entry"></param>
        /// <param name="utcExpiry"></param>
        public override void Set(string key, object entry, DateTime utcExpiry) {
            if (_dict.ContainsKey(key)) {
                _dict[key] = new CustomOutputCacheItem(entry, utcExpiry);
            }
            else {
                _dict.Add(key, new CustomOutputCacheItem(entry, utcExpiry));
            }
        }

        /// <summary>
        /// Override method for the Async OutputCache Provider
        /// </summary>
        /// <param name="key"></param>
        public override void Remove(string key) {
            _dict.Remove(key);
        }
    }
}

