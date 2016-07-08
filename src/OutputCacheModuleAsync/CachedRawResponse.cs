namespace Microsoft.AspNet.OutputCache {
    using System;

    internal class CachedRawResponse {

        public Guid CachedVaryId { get; set; }

        public HttpRawResponse RawResponse { get; set; }

        public string KernelCacheUrl { get; set; }

        public HttpCachePolicySettings CachePolicy { get; set; }
    }
}

