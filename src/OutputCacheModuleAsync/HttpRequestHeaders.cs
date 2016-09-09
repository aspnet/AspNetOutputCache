using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNet.OutputCache {
    static class HttpRequestHeaders {
        public const string IfModifiedSite = "If-Modified-Since";
        public const string IfNoneMatch = "If-None-Match";
        public const string AcceptEncoding = "Accept-Encoding";
        public const string ContentEncoding = "Content-Encoding";
        public const string CacheControl = "Cache-Control";
        public const string Pragma = "Pragma";
        public const string Range = "Range";
    }
    static class CacheDirectives {
        public const string NoCache = "no-cache";
        public const string NoStore = "no-store";
        public const string MaxAge = "max-age=";
        public const string MinFresh = "min-fresh=";
        public const string CacheControl = "Cache-Control";
        public const string Pragma = "Pragma";
        public const string Range = "Range";
    }
}
