using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Caching;

namespace CustomOutputCacheProvider {

    public class CustomOutputCacheItem {
        public object Obj;
        public DateTime UtcExpiry;

        public CustomOutputCacheItem(object entry, DateTime utcExpiryIn) {
            Obj = entry;
            UtcExpiry = utcExpiryIn;
        }
    }

    public class CustomOutputCacheProvider : OutputCacheProviderAsync {

        private readonly Dictionary<string, CustomOutputCacheItem> _dict;

        public CustomOutputCacheProvider() {
            _dict = new Dictionary<string, CustomOutputCacheItem>();
        }

        private static async Task FooAsync() {
            await Task.Delay(1);
        }

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

        public override async Task SetAsync(string key, object entry, DateTime utcExpiry) {
            await FooAsync();
            if (_dict.ContainsKey(key)) {
                _dict[key] = new CustomOutputCacheItem(entry, utcExpiry);
            }
            else {
                _dict.Add(key, new CustomOutputCacheItem(entry, utcExpiry));
            }
        }

        public override async Task RemoveAsync(string key) {
            await FooAsync();
            _dict.Remove(key);
        }

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

        public override void Set(string key, object entry, DateTime utcExpiry) {
            if (_dict.ContainsKey(key)) {
                _dict[key] = new CustomOutputCacheItem(entry, utcExpiry);
            }
            else {
                _dict.Add(key, new CustomOutputCacheItem(entry, utcExpiry));
            }
        }

        public override void Remove(string key) {
            _dict.Remove(key);
        }
    }
}

