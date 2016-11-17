namespace Microsoft.AspNet.OutputCache {
    using System.Runtime.Caching;
    using System.Web.Caching;

    /// <summary>
    /// This class convert System.Web.Caching.CacheItemRemovedCallback into format of System.Runtime.Caching.CacheEntryRemovedCallback
    /// </summary>
    sealed class RemovedCallback {
        CacheItemRemovedCallback _callback;

        public RemovedCallback(CacheItemRemovedCallback callback) {
            _callback = callback;
        }

        public void CacheEntryRemovedCallback(CacheEntryRemovedArguments arguments) {
            string key = arguments.CacheItem.Key;
            object value = arguments.CacheItem.Value;
            CacheItemRemovedReason reason;
            switch (arguments.RemovedReason) {
                case CacheEntryRemovedReason.Removed:
                    reason = CacheItemRemovedReason.Removed;
                    break;
                case CacheEntryRemovedReason.Expired:
                    reason = CacheItemRemovedReason.Expired;
                    break;
                case CacheEntryRemovedReason.Evicted:
                    reason = CacheItemRemovedReason.Underused;
                    break;
                case CacheEntryRemovedReason.ChangeMonitorChanged:
                    reason = CacheItemRemovedReason.DependencyChanged;
                    break;
                default:
                    reason = CacheItemRemovedReason.Removed;
                    break;
            }
            _callback(key, value, reason);
        }
    }
}
