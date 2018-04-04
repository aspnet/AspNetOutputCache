// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System.Web.Caching;
    class OutputCacheFileResponseElement : FileResponseElement {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public OutputCacheFileResponseElement(string path, long offset, long length) : base(path, offset, length) {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="isImpersonating"></param>
        /// <param name="supportsLongTransmitFile"></param>
        public OutputCacheFileResponseElement(string path, long offset, long length, bool isImpersonating, bool supportsLongTransmitFile) : base(path, offset, length) {
            IsImpersonating = isImpersonating;
            SupportsLongTransmitFile = supportsLongTransmitFile;
        }
        /// <summary>
        /// 
        /// </summary>
        public bool IsImpersonating { get; }
        /// <summary>
        /// 
        /// </summary>
        public bool SupportsLongTransmitFile { get; }
    }
}