// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;

    [Serializable]
    sealed class CachedVary {
        public string[] ContentEncodings { get; set; }
        public string[] Headers { get; set; }
        public string[] Params { get; set; }
        public string VaryByCustom { get; set; }
        public bool VaryByAllParams { get; set; }
        public Guid CachedVaryId { get; set; }

        public CachedVary() {
            CachedVaryId = Guid.NewGuid();
        }

        public override bool Equals(object obj) {
            if (Object.ReferenceEquals(obj, this)) {
                return true;
            }
            var cv = obj as CachedVary;
            if (cv == null) {
                return false;
            }
            return VaryByAllParams == cv.VaryByAllParams
                   && VaryByCustom == cv.VaryByCustom
                   && StringUtil.StringArrayEquals(ContentEncodings, cv.ContentEncodings)
                   && StringUtil.StringArrayEquals(Headers, cv.Headers)
                   && StringUtil.StringArrayEquals(Params, cv.Params);
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }
}
