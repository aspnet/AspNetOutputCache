// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;

    [Serializable]
    sealed class CachedRawResponse {

        public Guid CachedVaryId { get; set; }

        public HttpRawResponse RawResponse { get; set; }

        public string KernelCacheUrl { get; set; }

        public HttpCachePolicySettings CachePolicy { get; set; }
    }
}

