using System.Linq;

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections.Generic;
    using System.Web;

    internal class HttpCachePolicySettings {
        public HttpCacheability Cacheability { get; set; }
        public IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> ValidationCallbackInfo { get; set; }
        public bool IgnoreRangeRequests { get; set; }
        public string[] VaryByContentEncodings { get; set; }
        public string[] VaryByHeaders { get; set; }
        public string[] VaryByParams { get; set; }
        public bool IgnoreParams { get; set; }
        public DateTime UtcExpires { get; set; }
        public TimeSpan MaxAge { get; set; }
        public TimeSpan SlidingDelta { get; set; }
        public bool SlidingExpiration { get; set; }

        public bool ValidUntilExpires {
            get {
                return !SlidingExpiration && !GenerateLastModifiedFromFiles && !GenerateEtagFromFiles
                       && ValidationCallbackInfo == null;
            }
            set { }
        }

        public DateTime UtcLastModified { get; set; }
        public string ETag { get; set; }
        public bool GenerateLastModifiedFromFiles { get; set; }
        public bool GenerateEtagFromFiles { get; set; }
        public string VaryByCustom { get; set; }
        public DateTime UtcTimestampCreated { get; set; }

        public bool IsValidationCallbackSerializable() {
            return ValidationCallbackInfo.All(info => info.Key.Method.IsStatic);
        }

        public bool HasValidationPolicy() {
            return ValidUntilExpires
                   || GenerateLastModifiedFromFiles
                   || GenerateEtagFromFiles
                   || ValidationCallbackInfo != null;
        }
    }
}