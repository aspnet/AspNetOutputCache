namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Web.Caching;

    [Serializable]
    sealed class DependencyCacheEntry {
        public string RawResponseKey;
        public string KernelCacheUrl;
        public string ProviderName;
    }
}
