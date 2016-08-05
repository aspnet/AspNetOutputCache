namespace Microsoft.AspNet.OutputCache {
    using System.Collections;
    using System.Collections.Specialized;

    class HttpRawResponse {
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public NameValueCollection Headers { get; set; }
        public ArrayList Buffers { get; set; }
        public bool HasSubstBlocks { get; set; }
    }
}
