namespace Microsoft.AspNet.OutputCache {
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Globalization;
    using System.Web;
    using System.Web.Caching;
    using System.Collections.Specialized;
    using System.Web.Configuration;
    using System.Diagnostics;
    using System.Configuration;
    using Resource;
    /// <summary>
    /// OutputCache Async Module, this Module is able to use Async type of OutputCache Providers 
    /// </summary>
    public class OutputCacheModuleAsync : IHttpModule {
        private const string Asterisk = "*";
        private static readonly char[] s_fieldSeparators = { ',', ' ' };
        private readonly OutputCacheHelper _outputCacheHelper = new OutputCacheHelper();

        void IHttpModule.Init(HttpApplication app) {
            var cacheConfig = ConfigurationManager.GetSection("system.web/caching/outputCache") as OutputCacheSection;
            if (!cacheConfig.EnableOutputCache) {
                return;
            }
            app.AddOnResolveRequestCacheAsync(BeginOnResolveRequestCache, EndOnResolveRequestCache);
            app.AddOnUpdateRequestCacheAsync(BeginOnUpdateRequestCache, EndOnUpdateRequestCache);
        }

        /// <summary>
        /// Implement the IHTTPModule interface
        /// </summary>
        public void Dispose() { }

        private IAsyncResult BeginOnResolveRequestCache(object source, EventArgs e, AsyncCallback cb, object extraData) {
            return TaskAsyncHelper.BeginTask(() => OnEnterAsync(source, e), cb, extraData);
        }

        private static void EndOnResolveRequestCache(IAsyncResult result) {
            TaskAsyncHelper.EndTask(result);
        }

        private IAsyncResult BeginOnUpdateRequestCache(object source, EventArgs e, AsyncCallback cb, object extraData) {
            return TaskAsyncHelper.BeginTask(() => OnLeaveAsync(source, e), cb, extraData);
        }

        private static void EndOnUpdateRequestCache(IAsyncResult result) {
            TaskAsyncHelper.EndTask(result);
        }

        private async Task OnEnterAsync(object source, EventArgs eventArgs) {
            var app = (HttpApplication)source;
            HttpContext context = app.Context;
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (!IsHttpMethodSupported(request)) {
                return;
            }

            // Create a lookup key. Also store the key in global parameter _key to be used inside OnLeave() later   
            string key = OutputCacheHelper.CreateOutputCachedItemKey(context, null);

            // Lookup the cache vary using the key
            object item = await _outputCacheHelper.GetAsync(key);
            if (item == null) {
                return;
            }
            // 'item' may be one of the following:
            //  - a CachedVary object (if the object varies by something)
            //  - a CachedRawResponse object (i.e. it doesn't vary on anything)
            //  First assume it's a CacheVary
            var cachedVary = item as CachedVary;
            CachedItem cachedItem = await CheckCacheVaryAsync(cachedVary, context);
            if (cachedItem.DoReturn)
                return;

            // From this point on, we have an Raw Response entry to work with.
            var cachedRawResponse = (CachedRawResponse)cachedItem.Item;
            HttpCachePolicySettings settings = cachedRawResponse.CachePolicy;
            if (CheckCachedVary(request, cachedVary, settings)) {
                return;
            }
            if (settings.IgnoreRangeRequests) {
                if (IsRangeRequest(request)) {
                    return;
                }
            }

            if (CheckHeaders(settings, request, context)) {
                return;
            }
            if (await CheckValidityAsync(key, settings, context)) {
                return;
            }

            HttpRawResponse rawResponse = cachedRawResponse.RawResponse;
            if (!IsContentEncodingAcceptable(cachedVary, request, rawResponse)) {
                return;
            }
            UpdateCachedResponse(context, request, response, key, settings, rawResponse);

            // re-insert entry in kernel cache if necessary
            string originalCacheUrl = cachedRawResponse.KernelCacheUrl;
            if (originalCacheUrl != null) {
                OutputCacheUtility.SetupKernelCaching(originalCacheUrl, context.Response);
            }
            app.CompleteRequest();
        }

        private async Task OnLeaveAsync(object source, EventArgs eventArgs) {
            HttpContext context = ((HttpApplication)source).Context;
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            //Determine whether the response is cacheable.
            if (!IsResponseCacheable(response, request, context)) {
                return;
            }
            await CacheResponseAsync(context, response);
        }

        private static bool IsResponseCacheable(HttpResponse response, HttpRequest request, HttpContext context) {
            HttpCachePolicy cache = response.Cache;
            if (!cache.IsModified()) {
                return false;
            }
            if (response.StatusCode != 200) {
                return false;
            }
            if (request.HttpMethod != HttpMethods.GET && request.HttpMethod != HttpMethods.POST) {
                return false;
            }
            if (response.HeadersWritten) {
                return false;
            }
            var cacheability = cache.GetCacheability();
            if (cacheability != HttpCacheability.Public &&
                cacheability != HttpCacheability.ServerAndPrivate &&
                cacheability != HttpCacheability.ServerAndNoCache) {
                return false;
            }
            if (cache.GetNoServerCaching()) {
                return false;
            }

            if (OutputCacheHelper.ContainsNonShareableCookies(response)) {
                return false;
            }
            bool hasExpirationPolicy = !cache.HasSlidingExpiration() &&
                                       (cache.GetExpires() != DateTime.MinValue || cache.GetMaxAge() != TimeSpan.Zero);
            bool hasValidationPolicy = cache.GetLastModifiedFromFileDependencies() ||
                                       cache.GetETagFromFileDependencies() ||
                                       OutputCacheUtility.GetValidationCallbacks(response).Any() ||
                                       (cache.IsValidUntilExpires() && !cache.HasSlidingExpiration());
            if (!hasExpirationPolicy && !hasValidationPolicy) {
                return false;
            }
            if (cache.VaryByHeaders[Asterisk]) {
                return false;
            }
            bool acceptParams = (cache.VaryByParams.IgnoreParams ||
                                 (Equals(cache.VaryByParams.GetParams(), new[] { "*" })) ||
                                 (cache.VaryByParams.GetParams() != null && cache.VaryByParams.GetParams().Any()));
            if (!acceptParams && (request.HttpMethod == HttpMethods.POST || (request.QueryString.Count > 0))) {
                return false;
            }
            return cache.VaryByContentEncodings.GetContentEncodings() == null ||
                   OutputCacheHelper.IsCacheableEncoding(context.Response.ContentEncoding.ToString(),
                       cache.VaryByContentEncodings.GetContentEncodings());
        }

        private async Task CacheResponseAsync(HttpContext context, HttpResponse response) {
            CachedVary cachedVary = null; ;
            string keyRawResponse;
            /* Add response to cache.*/
            OutputCacheHelper.UpdateCachedHeaders(response);
            //look at response cachepolicy and decide if to cache it
            HttpCachePolicySettings settings = OutputCacheHelper.GetCurrentSettings(response);
            string[] varyByHeaders = settings.VaryByHeaders;
            string[] varyByParams = settings.IgnoreParams ? null : settings.VaryByParams;
            /* Create the key if it was not created in OnEnter */
            string key = OutputCacheHelper.CreateOutputCachedItemKey(context, null);

            if (settings.VaryByContentEncodings == null && varyByHeaders == null && varyByParams == null &&
                settings.VaryByCustom == null) {
                // This is not a varyBy item.
                keyRawResponse = key;
            }
            else {
                /*
                 * There is a vary in the cache policy. We handle this
                 * by adding another item to the cache which contains
                 * a list of the vary headers. A request for the item
                 * without the vary headers in the key will return this 
                 * item. From the headers another key can be constructed
                 * to lookup the item with the raw response.
                 */
                if (varyByHeaders != null) {
                    for (int i = 0, n = varyByHeaders.Length; i < n; i++) {
                        varyByHeaders[i] = "HTTP_" + CultureInfo.InvariantCulture.TextInfo.ToUpper(
                            varyByHeaders[i].Replace('-', '_'));
                    }
                }
                bool varyByAllParams = false;
                if (varyByParams != null) {
                    varyByAllParams = (varyByParams.Length == 1 && varyByParams[0] == Asterisk);
                    if (varyByAllParams) {
                        varyByParams = null;
                    }
                    else {
                        for (int i = 0, n = varyByParams.Length; i < n; i++) {
                            varyByParams[i] = CultureInfo.InvariantCulture.TextInfo.ToLower(varyByParams[i]);
                        }
                    }
                }
                cachedVary = new CachedVary {
                    ContentEncodings = settings.VaryByContentEncodings,
                    Headers = varyByHeaders,
                    Params = varyByParams,
                    VaryByAllParams = varyByAllParams,
                    VaryByCustom = settings.VaryByCustom
                };
                keyRawResponse = OutputCacheHelper.CreateOutputCachedItemKey(context, cachedVary);
                if (keyRawResponse == null) {
                    return;
                }
                // it is possible that the user code calculating custom vary-by
                // string would Flush making the response non-cacheable. Check fo it here.
                if (response.HeadersWritten) {
                    return;
                }
            }
            DateTime utcExpires = Cache.NoAbsoluteExpiration;
            TimeSpan slidingDelta = Cache.NoSlidingExpiration;
            if (settings.SlidingExpiration) {
                slidingDelta = settings.SlidingDelta;
            }
            else if (settings.MaxAge != TimeSpan.MinValue) {
                DateTime utcTimestamp = (settings.UtcTimestampCreated != DateTime.MinValue)
                    ? settings.UtcTimestampCreated
                    : context.Timestamp;
                utcExpires = utcTimestamp + settings.MaxAge;
            }
            else if (settings.UtcExpires != DateTime.MinValue) {
                utcExpires = settings.UtcExpires;
            }
            // Check and ensure that item hasn't expired:
            await InsertResponseAsync(key, utcExpires, response, context, cachedVary, settings, keyRawResponse, slidingDelta);
        }
        private async Task InsertResponseAsync(string key, DateTime utcExpires, HttpResponse response, HttpContext context, CachedVary cachedVary, HttpCachePolicySettings settings, string keyRawResponse, TimeSpan slidingDelta) {
            if (utcExpires > DateTime.Now) {
                // Create the response object to be sent on cache hits.
                HttpRawResponse httpRawResponse = OutputCacheHelper.GetSnapshot(response);
                string kernelCacheUrl = OutputCacheUtility.SetupKernelCaching(null, context.Response);
                Guid cachedVaryId = cachedVary?.CachedVaryId ?? Guid.Empty;
                var cachedRawResponse = new CachedRawResponse {
                    RawResponse = httpRawResponse,
                    CachePolicy = settings,
                    KernelCacheUrl = kernelCacheUrl,
                    CachedVaryId = cachedVaryId
                };
                using (CacheDependency dep = OutputCacheUtility.CreateCacheDependency(context.Response)) {
                    await _outputCacheHelper.InsertResponseAsync(key, cachedVary,
                        keyRawResponse, cachedRawResponse,
                        dep,
                        utcExpires, slidingDelta);
                }
            }
        }

        private static void UpdateCachedResponse(HttpContext context, HttpRequest request, HttpResponse response,
            string key, HttpCachePolicySettings settings, HttpRawResponse rawResponse) {
            /*
             * Try to satisfy a conditional request. The cached response
             * must satisfy all conditions that are present.
             * 
             * We can only satisfy a conditional request if the response
             * is buffered and has no substitution blocks.
             * 
             * N.B. RFC 2616 says conditional requests only occur 
             * with the GET method, but we try to satisfy other
             * verbs (HEAD, POST) as well.
             */
            int send304 = -1;
            if (!rawResponse.HasSubstBlocks) {
                /* Check "If-Modified-Since" header */

                string ifModifiedSinceHeader = request.Headers[HttpRequestHeaders.IfModifiedSite];
                if (ifModifiedSinceHeader != null) {
                    send304 = 0;
                    DateTime utcIfModifiedSince = HttpDate.UtcParse(ifModifiedSinceHeader);
                    if (settings.UtcLastModified != DateTime.MinValue &&
                        settings.UtcLastModified <= utcIfModifiedSince &&
                        utcIfModifiedSince <= context.Timestamp.ToUniversalTime()) {
                        send304 = 1;
                    }
                }
                /* Check "If-None-Match" header */
                if (send304 != 0) {
                    string etag = request.Headers[HttpRequestHeaders.IfNoneMatch];
                    if (etag != null) {
                        send304 = 0;
                        string[] etags = etag.Split(s_fieldSeparators);
                        for (int i = 0, n = etags.Length; i < n; i++) {
                            if (i == 0 && etags[i].Equals(Asterisk)) {
                                send304 = 1;
                                break;
                            }
                            if (!etags[i].Equals(settings.ETag)) {
                                continue;
                            }
                            send304 = 1;
                            break;
                        }
                    }
                }
            }
            if (send304 == 1) {
                /*
                 * Send 304 Not Modified
                 */
                if (response.HeadersWritten) {
                    response.ClearHeaders();
                }
                response.Clear();
                response.StatusCode = 304;
            }
            else {
                // Check and see if the cachedRawResponse is from the disk
                // If so, we must clone the HttpRawResponse before sending it
                // UseSnapshot calls ClearAll
                OutputCacheHelper.UseSnapshot(rawResponse, request.HttpMethod != "HEAD", response);
            }
            OutputCacheHelper.ResetFromHttpCachePolicySettings(settings, context.Timestamp, response);
        }

        private static bool IsContentEncodingAcceptable(CachedVary cachedVary, HttpRequest request,
            HttpRawResponse rawResponse) {
            // ensure Content-Encoding is acceptable
            if (cachedVary?.ContentEncodings != null) {
                return true;
            }
            string acceptEncoding = request.Headers[HttpRequestHeaders.AcceptEncoding];
            NameValueCollection headers = rawResponse.Headers;
            if (headers == null) {
                return OutputCacheHelper.IsAcceptableEncoding(null, acceptEncoding);
            }
            string contentEncoding = headers.Cast<string>().FirstOrDefault(h => h == HttpRequestHeaders.ContentEncoding);
            return OutputCacheHelper.IsAcceptableEncoding(contentEncoding, acceptEncoding);
        }

        private bool CheckHeaders(HttpCachePolicySettings settings, HttpRequest request, HttpContext context) {
            if (!settings.HasValidationPolicy()) {
                if (request.Headers[HttpRequestHeaders.CacheControl] != null) {
                    string[] cacheDirectives = request.Headers[HttpRequestHeaders.CacheControl].Split(s_fieldSeparators);
                    foreach (string directive in cacheDirectives) {
                        if (checkMaxAge(directive, settings, context))
                            return true;
                    }
                }
                string pragma = request.Headers[HttpRequestHeaders.Pragma];
                if (pragma == null) {
                    return false;
                }
                if (!pragma.Split(s_fieldSeparators).Any(t => t == null || t == CacheDirectives.NoCache)) {
                    return false;
                }
                return true;
            }
            return false;
        }
        private async Task<bool> CheckValidityAsync(string key, HttpCachePolicySettings settings, HttpContext context) {
            if (settings.ValidationCallbackInfo == null || !settings.ValidationCallbackInfo.Any()) {
                return false;
            }
            /*
            * Check if the item is still valid.
            */
            var validationStatus = HttpValidationStatus.Valid;
            HttpValidationStatus validationStatusFinal = validationStatus;
            foreach (KeyValuePair<HttpCacheValidateHandler, object> vci in settings.ValidationCallbackInfo) {
                vci.Key(context, vci.Value, ref validationStatus);
                switch (validationStatus) {
                    case HttpValidationStatus.Invalid:
                        await _outputCacheHelper.RemoveAsync(key, context);
                        return true;
                    case HttpValidationStatus.IgnoreThisRequest:
                        validationStatusFinal = HttpValidationStatus.IgnoreThisRequest;
                        break;
                    case HttpValidationStatus.Valid:
                        break;
                    default:
                        validationStatus = validationStatusFinal;
                        break;
                }
            }
            if (validationStatusFinal == HttpValidationStatus.IgnoreThisRequest) {
                return true;
            }
            return false;
        }


        private bool checkMaxAge(string directive, HttpCachePolicySettings settings, HttpContext context) {
            if (directive == CacheDirectives.NoCache || directive == CacheDirectives.NoStore) {
                return true;
            }
            if (directive.StartsWith(CacheDirectives.MaxAge)) {
                int maxage;
                try {
                    int.TryParse(directive.Substring(8), out maxage);
                }
                catch {
                    maxage = -1;
                }

                if (maxage < 0) {
                    return false;
                }
                int age =
                    (int)
                        ((context.Timestamp.Ticks - settings.UtcTimestampCreated.Ticks) /
                         TimeSpan.TicksPerSecond);
                if (age < maxage) {
                    return false;
                }
                return true;
            }
            if (!directive.StartsWith(CacheDirectives.MinFresh)) {
                return false;
            }
            int minfresh = -1;
            Int32.TryParse((directive.Substring(10)), out minfresh);

            if (minfresh < 0 || settings.UtcExpires == DateTime.MinValue || settings.SlidingExpiration) {
                return false;
            }
            int fresh =
                (int)((settings.UtcExpires.Ticks - context.Timestamp.Ticks) / TimeSpan.TicksPerSecond);
            if (fresh >= minfresh) {
                return false;
            }
            return true;
        }
        private static bool IsHttpMethodSupported(HttpRequest request) {
            // Check if the request can be resolved for this method.       
            switch (request.HttpMethod) {
                case HttpMethods.HEAD:
                case HttpMethods.GET:
                case HttpMethods.POST:
                    break;
                default:
                    return false;
            }
            return true;
        }

        private async Task<CachedItem> CheckCacheVaryAsync(CachedVary cachedVary, HttpContext context) {
            object item = null;
            // If we have one, create a new cache key for it (this is a must)
            if (cachedVary == null) {
                return new CachedItem() { DoReturn = false, Item = null };
            }
            /*
                 * This cached output has a Vary policy. Create a new key based 
                 * on the vary headers in cachedRawResponse and try again.
                 *
                 * Skip this step if it's a VaryByNone vary policy.
                 */
            string key = OutputCacheHelper.CreateOutputCachedItemKey(context, cachedVary);
            if (key == null) {
                return new CachedItem { DoReturn = true, Item = null };
            }
            if (cachedVary.ContentEncodings == null) {
                // With the new key, look up the in-memory key.
                // At this point, we've exhausted the lookups in memory for this item.
                item = await _outputCacheHelper.GetAsync(key);
            }
            else {
                bool identityIsAcceptable = true;
                string acceptEncoding = context.Request.Headers[HttpRequestHeaders.AcceptEncoding];
                if (acceptEncoding != null) {
                    string[] contentEncodings = cachedVary.ContentEncodings;
                    int startIndex = 0;
                    bool done = false;
                    while (!done) {
                        done = true;
                        int index = OutputCacheHelper.GetAcceptableEncoding(contentEncodings, startIndex, acceptEncoding);
                        if (index > -1) {
                            identityIsAcceptable = false;
                            // the client Accept-Encoding header contains an encoding that's in the VaryByContentEncoding list
                            item = await _outputCacheHelper.GetAsync(key + contentEncodings[index]);
                            if (item != null) {
                                continue;
                            }
                            startIndex = index + 1;
                            if (startIndex < contentEncodings.Length) {
                                done = false;
                            }
                        }
                        else if (index == -2) {
                            // the identity has a weight of 0 and is not acceptable
                            identityIsAcceptable = false;
                        }
                    }
                }
                // the identity should not be used if the client Accept-Encoding contains an entry in the VaryByContentEncoding list or "identity" is not acceptable
                if (item == null && identityIsAcceptable) {
                    item = await _outputCacheHelper.GetAsync(key);
                }
            }
            if (item != null && ((CachedRawResponse)item).CachedVaryId == cachedVary.CachedVaryId) {
                return new CachedItem { DoReturn = false, Item = item };
            }
            if (item != null) {
                // explicitly remove entry because _cachedVaryId does not match
                await _outputCacheHelper.RemoveAsync(key, context);
            }
            return new CachedItem { DoReturn = true, Item = item };
        }

        private static bool CheckCachedVary(HttpRequest request, CachedVary cachedVary,
           HttpCachePolicySettings settings) {
            // From this point on, we have an entry to work with.
            if (cachedVary == null && !settings.IgnoreParams) {
                // This cached output has no vary policy, so make sure it doesn't have a query string or form post.
                if (request.HttpMethod == HttpMethods.POST) {
                    return true;
                }
                if (request.QueryString.Count > 0) {
                    return true;
                }
            }
            return false;
        }

        private static bool IsRangeRequest(HttpRequest request) {
            // Don't record this if as a cache miss. The response for a range request is not cached, and so
            // we don't want to pollute the cache hit/miss ratio.
            string rangeHeader = request.Headers[HttpRequestHeaders.Range];
            if (rangeHeader.StartsWith("bytes", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            return false;
        }

    }

    class CachedItem {
        public bool DoReturn { get; set; }
        public object Item { get; set; }
    }
}