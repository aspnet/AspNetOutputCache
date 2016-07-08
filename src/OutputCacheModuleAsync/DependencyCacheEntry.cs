namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Web.Caching;

    internal class DependencyCacheEntry {
        public string RawResponseKey;
        public string KernelCacheUrl;
        public string Name;
    }

    internal class DependencyCacheEntryWrapper {
        public DependencyCacheEntry DependencyCacheEntry;
        public CacheDependency Dependencies;
        public TimeSpan DependencyCacheTimeSpan;
        public CacheItemPriority CacheItemPriority;
        public CacheItemRemovedCallback DependencyRemovedCallback;
    }
}
