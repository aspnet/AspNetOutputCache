namespace Microsoft.AspNet.OutputCache {
    static class HttpRequestHeaders {
        public const string IfModifiedSince = "If-Modified-Since";
        public const string IfNoneMatch = "If-None-Match";
        public const string AcceptEncoding = "Accept-Encoding";
        public const string ContentEncoding = "Content-Encoding";
        public const string CacheControl = "Cache-Control";
        public const string Pragma = "Pragma";
        public const string Range = "Range";
        public const string HeaderServer = "HttpWorkerRequest.HeaderServer";
        public const string HeaderSetCookie = "HttpWorkerRequest.HeaderSetCookie";
        public const string HeaderCacheControl = "HttpWorkerRequest.HeaderCacheControl";
        public const string HeaderExpires = "HttpWorkerRequest.HeaderExpires";
        public const string HeaderLastModified = "HttpWorkerRequest.HeaderLastModified";
        public const string HeaderEtag = "HttpWorkerRequest.HeaderEtag";
        public const string HeaderVary = "HttpWorkerRequest.HeaderVary";
    }
}
