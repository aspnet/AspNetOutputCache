namespace Microsoft.AspNet.OutputCache {
    using System;

    class CachedVary {
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
            if (Object.ReferenceEquals(obj,this)) {
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
