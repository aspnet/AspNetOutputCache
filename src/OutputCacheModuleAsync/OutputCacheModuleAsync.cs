using System.Text;

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
        private static readonly char[] s_fieldSeparators = {',', ' '};
        private string _key;
        private readonly OutputCacheHelper _outputCacheHelper = new OutputCacheHelper();

        void IHttpModule.Init(HttpApplication app) {
            var cacheConfig = ConfigurationManager.GetSection("system.web/caching/outputCache") as OutputCacheSection;
            Debug.Assert(cacheConfig != null, "cacheConfig != null");
            if (!cacheConfig.EnableOutputCache) {
                return;
            }
            app.AddOnResolveRequestCacheAsync(BeginOnResolveRequestCache, EndOnResolveRequestCache);
            app.AddOnUpdateRequestCacheAsync(BeginOnUpdateRequestCache, EndOnUpdateRequestCache);
        }

        /// <summary>
        /// Implement the IHTTPModule interface
        /// </summary>
        public void Dispose() {}

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
            var app = (HttpApplication) source;
            HttpContext context = app.Context;
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (!IsValidHttpMethod(request)) {
                return;
            }

            // Create a lookup key. Also store the key in global parameter _key to be used inside OnLeave() later   
            string key = _key = OutputCacheHelper.CreateOutputCachedItemKeyAsync(context, null);

            // Lookup the cache vary using the key
            object item = await _outputCacheHelper.Get(key);
            if (await _outputCacheHelper.Get(key) == null) {
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
            var cachedRawResponse = (CachedRawResponse) cachedItem.Item;
            HttpCachePolicySettings settings = cachedRawResponse.CachePolicy;
            if (CheckHasQueryStringOrFormPost(request, key, cachedVary, settings)) {
                return;
            }

            if (await CheckHeadersToDetermineAcceptCachedCopy(key, settings, request, context)) {
                return;
            }

            HttpRawResponse rawResponse = cachedRawResponse.RawResponse;
            if (!EnsureContentEncodingAcceptable(cachedVary, request, rawResponse)) {
                return;
            }
            GetCachedResponse(context, request, response, key, settings, rawResponse);

            // re-insert entry in kernel cache if necessary
            string originalCacheUrl = cachedRawResponse.KernelCacheUrl;
            if (originalCacheUrl != null) {
                OutputCacheUtility.SetupKernelCaching(originalCacheUrl, context.Response);
            }
            _key = null;
            app.CompleteRequest();
        }

        private async Task OnLeaveAsync(object source, EventArgs eventArgs) {
            HttpContext context = ((HttpApplication) source).Context;
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;
            //Determine whether the response is cacheable.
            if (!IsResponseCacheable(response, request, context)) {
                return;
            }
            await CacheResponse(context, response);
            _key = null;
        }

        private static bool IsResponseCacheable(HttpResponse response, HttpRequest request, HttpContext context) {
            HttpCachePolicy cache = response.Cache;
            if (!cache.IsModified()) {
                return false;
            }
            if (response.StatusCode != 200) {
                return false;
            }
            if (request.HttpMethod != "GET" && request.HttpMethod != "POST") {
                return false;
            }
            if (response.HeadersWritten) {
                return false;
            }
            if (cache.GetCacheability() != HttpCacheability.Public &&
                cache.GetCacheability() != HttpCacheability.ServerAndPrivate &&
                cache.GetCacheability() != HttpCacheability.ServerAndNoCache) {
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
            if (cache.VaryByHeaders["*"]) {
                return false;
            }
            bool acceptParams = (cache.VaryByParams.IgnoreParams ||
                                 (Equals(cache.VaryByParams.GetParams(), new[] {"*"})) ||
                                 (cache.VaryByParams.GetParams() != null && cache.VaryByParams.GetParams().Any()));
            if (!acceptParams && (request.HttpMethod == "POST" || (request.QueryString.Count > 0))) {
                return false;
            }
            return cache.VaryByContentEncodings.GetContentEncodings() == null ||
                   OutputCacheHelper.IsCacheableEncoding(context.Response.ContentEncoding.ToString(),
                       cache.VaryByContentEncodings.GetContentEncodings());
        }

        private async Task CacheResponse(HttpContext context, HttpResponse response) {
            CachedVary cachedVary;
            string keyRawResponse;
            /*
            * Add response to cache.
            */
            OutputCacheHelper.UpdateCachedHeaders(response);
            //look at response cachepolicy and decide if to cache it
            HttpCachePolicySettings settings = OutputCacheHelper.GetCurrentSettings(response);
            string[] varyByHeaders = settings.VaryByHeaders;
            string[] varyByParams = settings.IgnoreParams ? null : settings.VaryByParams;
            /* Create the key if it was not created in OnEnter */
            if (_key == null) {
                _key = OutputCacheHelper.CreateOutputCachedItemKeyAsync(context, null);
                Debug.Assert(_key != null, "_key != null");
            }
            if (settings.VaryByContentEncodings == null && varyByHeaders == null && varyByParams == null &&
                settings.VaryByCustom == null) {
                // This is not a varyBy item.
                keyRawResponse = _key;
                cachedVary = null;
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
                keyRawResponse = OutputCacheHelper.CreateOutputCachedItemKeyAsync(context, cachedVary);
                if (keyRawResponse == null) {
                    Debug.WriteLine(SR.OutputCacheModuleLeave, string.Format(SR.Couldnot_add_non_cacheable_post,_key));
                    return;
                }
                // it is possible that the user code calculating custom vary-by
                // string would Flush making the response non-cacheable. Check fo it here.
                if (response.HeadersWritten) {
                    Debug.WriteLine(SR.OutputCacheModuleLeave,
                        string.Format(SR.Response_Flush_inside_GetVaryByCustomstring, _key));
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
                Debug.WriteLine(SR.OutputCacheModuleLeave, string.Format(SR.Adding_response_to_cache, keyRawResponse));
                CacheDependency dep = OutputCacheUtility.CreateCacheDependency(context.Response);
                try {
                    await _outputCacheHelper.InsertResponse(_key, cachedVary,
                        keyRawResponse, cachedRawResponse,
                        dep,
                        utcExpires, slidingDelta);
                }
                catch {
                    dep?.Dispose();
                    throw;
                }
            }
        }

        private static void GetCachedResponse(HttpContext context, HttpRequest request, HttpResponse response,
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
                string ifModifiedSinceHeader = request.Headers["If-Modified-Since"];
                if (ifModifiedSinceHeader != null) {
                    send304 = 0;
                    try {
                        DateTime utcIfModifiedSince = HttpDate.UtcParse(ifModifiedSinceHeader);
                        if (settings.UtcLastModified != DateTime.MinValue &&
                            settings.UtcLastModified <= utcIfModifiedSince &&
                            utcIfModifiedSince <= context.Timestamp.ToUniversalTime()) {
                            send304 = 1;
                        }
                    }
                    catch {
                        Debug.WriteLine(SR.OutputCacheModuleEnter,
                            string.Format(SR.Ignore_IfModifiedSince_header, ifModifiedSinceHeader));
                    }
                }
                /* Check "If-None-Match" header */
                if (send304 != 0) {
                    string etag = request.Headers["IfNoneMatch"];
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
                Debug.WriteLine(SR.OutputCacheModuleEnter, string.Format(SR.Hit_conditional_request_satisfied,key) +"OutputCacheModule::Enter");
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

        private static bool EnsureContentEncodingAcceptable(CachedVary cachedVary, HttpRequest request,
            HttpRawResponse rawResponse) {
            // ensure Content-Encoding is acceptable
            if (cachedVary?.ContentEncodings != null) {
                return true;
            }
            string acceptEncoding = request.Headers["Accept-Encoding"];
            NameValueCollection headers = rawResponse.Headers;
            if (headers == null) {
                return OutputCacheHelper.IsAcceptableEncoding(null, acceptEncoding);
            }
            string contentEncoding = headers.Cast<string>().FirstOrDefault(h => h == "Content-Encoding");
            return OutputCacheHelper.IsAcceptableEncoding(contentEncoding, acceptEncoding);
        }

        private async Task<bool> CheckHeadersToDetermineAcceptCachedCopy(string key, HttpCachePolicySettings settings,
            HttpRequest request, HttpContext context) {
            if (!settings.HasValidationPolicy()) {
                if (request.Headers["Cache-Control"] != null) {
                    string[] cacheDirectives = request.Headers["Cache-Control"].Split(s_fieldSeparators);
                    foreach (string directive in cacheDirectives) {
                        if (directive == "no-cache" || directive == "no-store") {
                            Debug.WriteLine(SR.OutputCacheModuleEnter,
                                SR.Skipping_lookup_because_of_Cache_Control_no_cache_or_no_store_directive + "OutputCacheModule::Enter");
                            return true;
                        }
                        if (directive.StartsWith("max-age=")) {
                            int maxage;
                            try {
                                int.TryParse(directive.Substring(8), out maxage);
                            }
                            catch {
                                maxage = -1;
                            }

                            if (maxage < 0) {
                                continue;
                            }
                            int age =
                                (int)
                                    ((context.Timestamp.Ticks - settings.UtcTimestampCreated.Ticks)/
                                     TimeSpan.TicksPerSecond);
                            if (age < maxage) {
                                continue;
                            }
                            Debug.WriteLine(SR.OutputCacheModuleEnter,
                                SR.Not_returning_found_item_due_to_Cache_Control_max_age_directive + "OutputCacheModule::Enter");
                            return true;
                        }
                        if (!directive.StartsWith("min-fresh=")) {
                            continue;
                        }
                        int minfresh;
                        try {
                            minfresh = Convert.ToInt32(directive.Substring(10), CultureInfo.InvariantCulture);
                        }
                        catch {
                            minfresh = -1;
                        }

                        if (minfresh < 0 || settings.UtcExpires == DateTime.MinValue || settings.SlidingExpiration) {
                            continue;
                        }
                        int fresh =
                            (int) ((settings.UtcExpires.Ticks - context.Timestamp.Ticks)/TimeSpan.TicksPerSecond);
                        if (fresh >= minfresh) {
                            continue;
                        }
                        Debug.WriteLine(SR.OutputCacheModuleEnter,
                           SR.Not_returning_found_item_due_to_Cache_Control_min_fresh_directive + "OutputCacheModule::Enter");
                        return true;
                    }
                }
                string pragma = request.Headers["Pragma"];
                if (pragma == null) {
                    return false;
                }
                string[] pragmaDirectives = pragma.Split(s_fieldSeparators);
                if (!pragmaDirectives.Any(t => t == null || t == "no-cache")) {
                    return false;
                }
                Debug.WriteLine(SR.OutputCacheModuleEnter,
                   SR.Skipping_lookup_because_of_Pragma_no_cache_directive + " OutputCacheModule::Enter");
                return true;
            }
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
                        Debug.WriteLine(SR.OutputCacheModuleEnter,
                          string.Format(SR.Output_cache_item_found_but_callback_invalidated_it,key) + " OutputCacheModule::Enter");

                        await _outputCacheHelper.Remove(key, context);
                        return true;
                    case HttpValidationStatus.IgnoreThisRequest:
                        validationStatusFinal = HttpValidationStatus.IgnoreThisRequest;
                        break;
                    case HttpValidationStatus.Valid:
                        break;
                    default:
                        Debug.WriteLine(SR.OutputCacheModuleEnter,
                            string.Format(SR.Invalid_validation_status, validationStatus, key));
                        validationStatus = validationStatusFinal;
                        break;
                }
            }

            if (validationStatusFinal == HttpValidationStatus.IgnoreThisRequest) {
                Debug.WriteLine(SR.OutputCacheModuleEnter,
                   string.Format(SR.Callback_status_is_IgnoreThisRequest, key) + " OutputCacheModule::Enter");
                return true;
            }

            Debug.Assert(validationStatusFinal == HttpValidationStatus.Valid,
                "validationStatusFinal == HttpValidationStatus.Valid");
            return false;
        }

        private static bool IsValidHttpMethod(HttpRequest request) {
            // Check if the request can be resolved for this method.       
            switch (request.HttpMethod) {
                case "HEAD":
                case "GET":
                case "POST":
                    break;
                default:
                    Debug.WriteLine("Http "
                        +SR.method_is_not + "GET, POST, or HEAD."+ SR.Returning_from + " OutputCacheModule::Enter");
                    return false;
            }
            return true;
        }

        private async Task<CachedItem> CheckCacheVaryAsync(CachedVary cachedVary, HttpContext context) {
            object item = null;
            // If we have one, create a new cache key for it (this is a must)
            if (cachedVary == null) {
                return new CachedItem() {DoReturn = false, Item = null};
            }
            /*
                 * This cached output has a Vary policy. Create a new key based 
                 * on the vary headers in cachedRawResponse and try again.
                 *
                 * Skip this step if it's a VaryByNone vary policy.
                 */
            string key = OutputCacheHelper.CreateOutputCachedItemKeyAsync(context, cachedVary);
            if (key == null) {
                Debug.WriteLine(SR.OutputCacheModuleEnter,
                   string.Format(SR.Miss_key_could_not_be_created_for_varyby_item,"Vary-By") + " OutputCacheModule::Enter");
                return new CachedItem {DoReturn = true, Item = null};
            }
            if (cachedVary.ContentEncodings == null) {
                // With the new key, look up the in-memory key.
                // At this point, we've exhausted the lookups in memory for this item.
                item = await _outputCacheHelper.Get(key);
            }
            else {
                bool identityIsAcceptable = true;
                string acceptEncoding = context.Request.Headers.GetKey(22);
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
                            item = await _outputCacheHelper.Get(key + contentEncodings[index]);
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
                    item = await _outputCacheHelper.Get(key);
                }
            }
            if (item != null && ((CachedRawResponse) item).CachedVaryId == cachedVary.CachedVaryId) {
                return new CachedItem {DoReturn = false, Item = item};
            }
            if (item != null) {
                // explicitly remove entry because _cachedVaryId does not match
                await _outputCacheHelper.Remove(key, context);
            }
            return new CachedItem {DoReturn = true, Item = item};
        }

        private static bool CheckHasQueryStringOrFormPost(HttpRequest request, string key, CachedVary cachedVary,
            HttpCachePolicySettings settings) {
            // From this point on, we have an entry to work with.
            if (cachedVary == null && !settings.IgnoreParams) {
                // This cached output has no vary policy, so make sure it doesn't have a query string or form post.
                if (request.HttpMethod == "POST") {
                    Debug.WriteLine(SR.OutputCacheModuleEnter,
                       string.Format(SR.Method_is_POST_and_no_VaryByParam_specified, "POST", "VaryByParam", key) +
                        " OutputCacheModule::Enter");
                    return true;
                }
                if (request.QueryString.Count > 0) {
                    Debug.WriteLine(SR.OutputCacheModuleEnter,
                       string.Format(SR.Contains_querystring_and_no_VaryByParam_specified, " querystring ", " VaryByParam ", key) +
                        " OutputCacheModule::Enter");
                    return true;
                }
            }
            if (!settings.IgnoreRangeRequests) {
                return false;
            }
            string rangeHeader = request.Headers["Range"];
            if (!rangeHeader.StartsWith("bytes", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            Debug.WriteLine(SR.OutputCacheModuleEnter,
               string.Format(SR.Range_request_and_IgnoreRangeRequests_is_true, " Range request ", " IgnoreRangeRequests ", key) +
                " OutputCacheModule::Enter");
            // Don't record this as a cache miss. The response for a range request is not cached, and so
            // we don't want to pollute the cache hit/miss ratio.
            return true;
        }
    }

    internal class CachedItem {
        public bool DoReturn { get; set; }
        public object Item { get; set; }
    }
}