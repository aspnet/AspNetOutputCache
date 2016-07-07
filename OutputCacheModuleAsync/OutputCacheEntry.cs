namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections;
    using System.Collections.Specialized;

    internal class OutputCacheEntry {
        public Guid CachedVaryId { get; set; }
        public HttpCachePolicySettings Settings { get; set; }
        public string KernelCacheUrl { get; set; }
        public string DependenciesKey { get; set; }
        public string[] Dependencies { get; set; }
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public NameValueCollection HeaderElements { get; set; }
        public ArrayList ResponseElements { get; set; }
    }
}