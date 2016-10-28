namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Web.Caching;

    sealed class DependencyCacheEntry {
        public string RawResponseKey;
        public string KernelCacheUrl;
        public string Name;
    }

    sealed class DependencyCacheEntryWrapper {
        public DependencyCacheEntry DependencyCacheEntry;
        public CacheDependency Dependencies;
        public TimeSpan DependencyCacheTimeSpan;
        public CacheItemPriority CacheItemPriority;
        public CacheItemRemovedCallback DependencyRemovedCallback;
    }
}
