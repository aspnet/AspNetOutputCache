// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Web.Caching;

    [Serializable]
    sealed class OutputCacheEntry {
        public Guid CachedVaryId { get; set; }
        public HttpCachePolicySettings Settings { get; set; }
        public string KernelCacheUrl { get; set; }
        public string DependenciesKey { get; set; }
        public string[] Dependencies { get; set; }
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public NameValueCollection HeaderElements { get; set; }
        public IEnumerable<ResponseElement> ResponseBuffers { get; set; }
    }
}