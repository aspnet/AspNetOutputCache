// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;

    [Serializable]
    sealed class DependencyCacheEntry {
        public string RawResponseKey;
        public string KernelCacheUrl;
        public string ProviderName;
    }
}
