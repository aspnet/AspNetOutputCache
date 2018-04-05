// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections;
    using System.Collections.Specialized;

    [Serializable]
    sealed class HttpRawResponse {
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public NameValueCollection Headers { get; set; }
        public ArrayList Buffers { get; set; }
    }
}
