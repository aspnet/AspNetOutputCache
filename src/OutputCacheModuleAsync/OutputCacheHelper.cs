﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using System.Threading.Tasks;
    using System.Web.Caching;
    using System.Collections;
    using System.Web;
    using System.Collections.Specialized;
    using System.Text;
    using System.Globalization;
    using System.IO;
    using Resource;
    using System.Runtime.Caching;

    sealed class OutputCacheHelper {
        #region private fileds
        private readonly char[] s_fieldSeparators = { ',', ' ' };
        private const int MaxPostKeyLength = 15000;
        private const string NullVarybyValue = "+n+";
        private const string OutputcacheKeyprefixPost = "a1";
        private const string OutputcacheKeyprefixGet = "a2";
        private const string Identity = "identity";
        private const string Asterisk = "*";
        private const string OutputcacheKeyprefixDependencies = "Microsoft.AspNet.OutputCache.Dependencies";
        private static CacheItemRemovedCallback s_dependencyRemovedCallback;
        private static MemoryCache memoryCache = new MemoryCache("Microsoft.AspNet.OutputCache.MemoryCache");
        private static InMemoryOutputCacheProvider inMemoryOutputCacheProvider = new InMemoryOutputCacheProvider();
        private static Converter converter = new Converter();
        private HttpContextBase _context;
        private IOutputCacheUtility _cacheUtility;
        #endregion

        public OutputCacheHelper(HttpContextBase httpContext) : this(httpContext, new OutputCacheUtilityAdapter())
        {
        }

        internal OutputCacheHelper(HttpContextBase httpContext, IOutputCacheUtility cacheUtil)
        {
            _context = httpContext;
            s_dependencyRemovedCallback = new CacheItemRemovedCallback(DependencyRemovedCallback);
            _cacheUtility = cacheUtil;
        }

        #region public methods
        public bool IsContentEncodingAcceptable(CachedVary cachedVary, HttpRawResponse rawResponse) {
            // ensure Content-Encoding is acceptable
            if (cachedVary?.ContentEncodings != null) {
                return true;
            }
            var acceptEncoding = _context.Request.Headers[HttpHeaders.AcceptEncoding];
            var headers = rawResponse.Headers;
            if (headers == null) {
                return IsAcceptableEncoding(null, acceptEncoding);
            }
            var contentEncoding = headers.Cast<string>().FirstOrDefault(h => h.Equals(HttpHeaders.ContentEncoding, StringComparison.OrdinalIgnoreCase));
            return IsAcceptableEncoding(contentEncoding, acceptEncoding);
        }

        public bool CheckHeaders(HttpCachePolicySettings settings) {
            if (!settings.HasValidationPolicy()) {
                if (_context.Request.Headers[HttpHeaders.CacheControl] != null) {
                    var cacheDirectives = _context.Request.Headers[HttpHeaders.CacheControl].Split(s_fieldSeparators);
                    foreach (string directive in cacheDirectives) {
                        if (CheckMaxAge(directive, settings))
                            return true;
                    }
                }
                var pragma = _context.Request.Headers[HttpHeaders.Pragma];
                if (pragma == null) {
                    return false;
                }
                if (!pragma.Split(s_fieldSeparators).Any(t => t == null || t.Equals(CacheDirectives.NoCache, StringComparison.OrdinalIgnoreCase))) {
                    return false;
                }
                return true;
            }
            return false;
        }

        public async Task<bool> CheckValidityAsync(string key, HttpCachePolicySettings settings) {
            if (settings.ValidationCallbackInfo == null || !settings.ValidationCallbackInfo.Any()) {
                return false;
            }
            /*
            * Check if the item is still valid.
            */
            var validationStatus = HttpValidationStatus.Valid;
            var validationStatusFinal = validationStatus;
            foreach (KeyValuePair<HttpCacheValidateHandler, object> vci in settings.ValidationCallbackInfo) {
                vci.Key(_cacheUtility.GetContextFromHttpContextBase(_context), vci.Value, ref validationStatus);
                switch (validationStatus) {
                    case HttpValidationStatus.Invalid:
                        await RemoveAsync(key);
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
            return validationStatusFinal == HttpValidationStatus.IgnoreThisRequest;
        }

        public bool IsHttpMethodSupported() {
            // Check if the request can be resolved for this method.       
            switch (_context.Request.HttpMethod.ToUpper()) {
                case HttpMethods.HEAD:
                case HttpMethods.GET:
                case HttpMethods.POST:
                    break;
                default:
                    return false;
            }
            return true;
        }

        public async Task<object> GetAsCacheVaryAsync(CachedVary cachedVary) {
            object item = null;
            // If we have one, create a new cache key for it (this is a must)
            /*
                 * This cached output has a Vary policy. Create a new key based 
                 * on the vary headers in cachedRawResponse and try again.
                 *
                 * Skip this step if it's a VaryByNone vary policy.
                 */
            var key = CreateOutputCachedItemKey(cachedVary);
            if (key == null) {
                return null;
            }
            if (cachedVary.ContentEncodings == null) {
                // With the new key, look up the in-memory key.
                // At this point, we've exhausted the lookups in memory for this item.
                item = await GetAsync(key);
            }
            else {
                var identityIsAcceptable = true;
                var acceptEncoding = _context.Request.Headers[HttpHeaders.AcceptEncoding];
                if (acceptEncoding != null) {
                    var contentEncodings = cachedVary.ContentEncodings;
                    var startIndex = 0;
                    var done = false;

                    while (!done) {
                        done = true;
                        var index = GetAcceptableEncoding(contentEncodings, startIndex, acceptEncoding);
                        if (index > -1) {
                            identityIsAcceptable = false;
                            // the client Accept-Encoding header contains an encoding that's in the VaryByContentEncoding list
                            item = await GetAsync(key);
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
                    item = await GetAsync(key);
                }
            }
            if (item == null) return null;

            if (((CachedRawResponse)item).CachedVaryId == cachedVary.CachedVaryId) {
                return item;
            }
            else {
                // explicitly remove entry because _cachedVaryId does not match
                await RemoveAsync(key);
                return null;
            }
        }

        public bool CheckCachedVary(CachedVary cachedVary, HttpCachePolicySettings settings) {
            // From this point on, we have an entry to work with.
            if (cachedVary == null && !settings.IgnoreParams) {
                // This cached output has no vary policy, so make sure it doesn't have a query string or form post.
                if (_context.Request.HttpMethod.Equals(HttpMethods.POST, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
                if (_context.Request.QueryString.Count > 0) {
                    return true;
                }
            }
            return false;
        }

        public bool IsRangeRequest() {
            // Don't record this if as a cache miss. The response for a range request is not cached, and so
            // we don't want to pollute the cache hit/miss ratio.
            if(_context.Request.Headers[HttpHeaders.Range] == null) {
                return false;
            }
            return _context.Request.Headers[HttpHeaders.Range].StartsWith("bytes", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<object> GetAsync(string key) {
            var provider = GetProvider();
            var result = await provider.GetAsync(key);
            var oce = result as OutputCacheEntry;

            if (oce == null) {
                return result;
            }
            if (HasDependencyChanged(oce.DependenciesKey, oce.Dependencies, oce.KernelCacheUrl, key, provider.Name)) {
                await RemoveFromProvider(key, provider.Name);
                return null;
            }
            result = converter.CreateCachedRawResponse(oce);
            return result;
        }

        /*
        * Return a key to lookup a cached response. The key contains 
        * the path and optionally, vary parameters, vary headers, custom strings,
        * and form posted data.
        */
        public string CreateOutputCachedItemKey(CachedVary cachedVary) {
            return CreateOutputCachedItemKey(_context.Request.Path, _context.Request.HttpMethod, cachedVary);
        }

        /*
         * GetAcceptableEncoding finds an acceptable coding for the given
         * Accept-Encoding header (see RFC 2616)
         * returns either i) an acceptable index in contentEncodings, ii) -1 if the identity is acceptable, or iii) -2 if nothing is acceptable
         */
        public int GetAcceptableEncoding(string[] contentEncodings, int startIndex, string acceptEncoding) {
            // The format of Accept-Encoding is ( 1#( codings [ ";" "q" "=" qvalue ] ) | "*" )
            if (string.IsNullOrEmpty(acceptEncoding)) {
                return -1; // use "identity"
            }

            // is there only one token?
            var tokenEnd = acceptEncoding.IndexOf(',');
            if (tokenEnd == -1) {
                var acceptEncodingWithoutWeight = acceptEncoding;
                tokenEnd = acceptEncoding.IndexOf(';');

                if (tokenEnd > -1) {
                    // remove weight
                    var space = acceptEncoding.IndexOf(' ');
                    if (space > -1 && space < tokenEnd) {
                        tokenEnd = space;
                    }
                    acceptEncodingWithoutWeight = acceptEncoding.Substring(0, tokenEnd);
                    if (ParseWeight(acceptEncoding, tokenEnd) == 0) {
                        //weight is 0, use "identity" only if it is acceptable
                        var identityIsAcceptable = !acceptEncodingWithoutWeight.Equals(Identity, StringComparison.OrdinalIgnoreCase) &&
                                                    acceptEncodingWithoutWeight != Asterisk;
                        return (identityIsAcceptable) ? -1 : -2;
                    }
                }
                if (acceptEncodingWithoutWeight == Asterisk) {
                    // just return the index of the first entry in the list, since it is acceptable
                    return 0;
                }
                for (var i = startIndex; i < contentEncodings.Length; i++) {
                    if (string.Equals(contentEncodings[i], acceptEncodingWithoutWeight,
                        StringComparison.OrdinalIgnoreCase)) {
                        return i; // found
                    }
                }
                return -1; // not found, use "identity"
            }
            // there are multiple tokens
            var bestCodingIndex = -1;
            var bestCodingWeight = 0d;
            for (var i = startIndex; i < contentEncodings.Length; i++) {
                var coding = contentEncodings[i];
                // get weight of current coding
                var weight = GetAcceptableEncodingHelper(coding, acceptEncoding);
                // if it is 1, use it
                if (weight == 1) {
                    return i;
                }
                // if it is the best so far, remember it
                if (!(weight > bestCodingWeight)) {
                    continue;
                }
                bestCodingIndex = i;
                bestCodingWeight = weight;
            }
            // use "identity" only if it is acceptable
            if (bestCodingIndex == -1 && !IsIdentityAcceptable(acceptEncoding)) {
                bestCodingIndex = -2;
            }
            return bestCodingIndex; // coding index with highest weight, possibly -1 or -2
        }

        public bool IsAcceptableEncoding(string contentEncoding, string acceptEncoding) {
            if (string.IsNullOrEmpty(contentEncoding)) {
                // if Content-Encoding is not set treat it as the identity
                contentEncoding = Identity;
            }
            if (string.IsNullOrEmpty(acceptEncoding)) {
                // only the identity is acceptable if Accept-Encoding is not set
                return (contentEncoding.Equals(Identity, StringComparison.OrdinalIgnoreCase));
            }
            var weight = GetAcceptableEncodingHelper(contentEncoding, acceptEncoding);
            return !(weight == 0) &&
                   (!(weight <= 0) || GetAcceptableEncodingHelper(Asterisk, acceptEncoding) != 0);
        }

        public bool IsResponseCacheable() {
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);
            if (!cache.IsModified()) {
                return false;
            }
            if (_context.Response.StatusCode != 200) {
                return false;
            }
            if (!IsHttpMethodSupported()) {
                return false;
            }
            if (_context.Response.HeadersWritten) {
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

            if (ContainsNonShareableCookies()) {
                return false;
            }
            var hasExpirationPolicy = !cache.HasSlidingExpiration() &&
                                       (cache.GetExpires() != DateTime.MinValue || cache.GetMaxAge() != TimeSpan.Zero);
            var hasValidationPolicy = cache.GetLastModifiedFromFileDependencies() ||
                                       cache.GetETagFromFileDependencies() ||
                                       _cacheUtility.GetValidationCallbacks(_context).Any() ||
                                       (cache.IsValidUntilExpires() && !cache.HasSlidingExpiration());
            if (!hasExpirationPolicy && !hasValidationPolicy) {
                return false;
            }
            if (cache.VaryByHeaders[Asterisk]) {
                return false;
            }
            var acceptParams = (cache.VaryByParams.IgnoreParams ||
                                 (Equals(cache.VaryByParams.GetParams(), new[] { Asterisk })) ||
                                 (cache.VaryByParams.GetParams() != null && cache.VaryByParams.GetParams().Any()));
            if (!acceptParams && 
                (_context.Request.HttpMethod.Equals(HttpMethods.POST, StringComparison.OrdinalIgnoreCase) || 
                    (_context.Request.QueryString.Count > 0))) {
                return false;
            }
            return cache.VaryByContentEncodings.GetContentEncodings() == null ||
                   IsCacheableEncoding(_context.Request.Headers[HttpHeaders.AcceptEncoding],
                       cache.VaryByContentEncodings);
        }

        public async Task CacheResponseAsync() {
            CachedVary cachedVary = null; ;
            string keyRawResponse;
            /* Add response to cache.*/
            UpdateCachedHeaders();
            //look at response cachepolicy and decide if to cache it
            var settings = GetCurrentSettings();
            var varyByHeaders = settings.VaryByHeaders;
            var varyByParams = settings.IgnoreParams ? null : settings.VaryByParams;
            /* Create the key if it was not created in OnEnter */
            var key = CreateOutputCachedItemKey(null);

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
                var varyByAllParams = false;
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
                keyRawResponse = CreateOutputCachedItemKey(cachedVary);
                if (keyRawResponse == null) {
                    return;
                }
                // it is possible that the user code calculating custom vary-by
                // string would Flush making the response non-cacheable. Check fo it here.
                if (_context.Response.HeadersWritten) {
                    return;
                }
            }
            var utcExpires = Cache.NoAbsoluteExpiration;
            var slidingDelta = Cache.NoSlidingExpiration;
            if (settings.SlidingExpiration) {
                slidingDelta = settings.SlidingDelta;
            }
            else if (settings.MaxAge != TimeSpan.Zero) {
                var utcTimestamp = (settings.UtcTimestampCreated != DateTime.MinValue)
                    ? settings.UtcTimestampCreated
                    : _context.Timestamp;
                utcExpires = utcTimestamp + settings.MaxAge;
            }
            else if (settings.UtcExpires != DateTime.MinValue) {
                utcExpires = settings.UtcExpires;
            }
            // Check and ensure that item hasn't expired:
            await InsertResponseAsync(key, utcExpires, cachedVary, settings, keyRawResponse, slidingDelta);
        }

        public void UpdateCachedResponse(HttpCachePolicySettings settings, HttpRawResponse rawResponse) {
            /* Try to satisfy a conditional request. The cached response
            * must satisfy all conditions that are present.
            * We can only satisfy a conditional request if the response
            * is buffered and has no substitution blocks.
            * N.B. RFC 2616 says conditional requests only occur 
            * with the GET method, but we try to satisfy other
            * verbs (HEAD, POST) as well.*/
            if (CheckIfModifiedSince(settings) && CheckIfNoneMatch(settings)) {
                /*
                 * Send 304 Not Modified
                 */
                if (_context.Response.HeadersWritten) {
                    _context.Response.ClearHeaders();
                }
                _context.Response.Clear();
                _context.Response.StatusCode = 304;
            }
            else {
                // Check and see if the cachedRawResponse is from the disk
                // If so, we must clone the HttpRawResponse before sending it
                // UseSnapshot calls ClearAll
                UseSnapshot(rawResponse, !_context.Request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase));
            }
            ResetFromHttpCachePolicySettings(settings, _context.Timestamp);
        }

        public bool IsKernelCacheAPISupported() {
            // Check from reflection if Kernel Cache methods are supported
            var System_Web = typeof(ResponseElement).Assembly;
            if (System_Web != null) {
                var OutputCacheUtilityType = System_Web.GetType("System.Web.Caching.OutputCacheUtility");
                if (OutputCacheUtilityType != null && OutputCacheUtilityType.GetMethod("FlushKernelCache") != null) {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region private methods
        private async void DependencyRemovedCallback(string key, object value, CacheItemRemovedReason reason) {
            var dce = value as DependencyCacheEntry;
            // Invalidate kernel cache entry
            if (dce.KernelCacheUrl != null && IsKernelCacheAPISupported()) {
                var System_Web = typeof(ResponseElement).Assembly;
                var OutputCacheUtilityType = System_Web.GetType("System.Web.Caching.OutputCacheUtility");
                
                if (OutputCacheUtilityType != null) {
                    var flushKernelCacheMethod = OutputCacheUtilityType.GetMethod("FlushKernelCache");
                    if (flushKernelCacheMethod != null) {
                        flushKernelCacheMethod.Invoke(dce.KernelCacheUrl, new object[] {dce.KernelCacheUrl});
                    }
                }
            }
            if (reason == CacheItemRemovedReason.DependencyChanged) {
                if (dce.RawResponseKey != null) {
                    await RemoveFromProvider(dce.RawResponseKey, dce.ProviderName);
                }
            }
        }

        private async Task RemoveFromProvider(string key, string providerName) {
            var provider = GetProvider(providerName) ?? inMemoryOutputCacheProvider;

            await provider.RemoveAsync(key);
        }

        private OutputCacheProviderAsync GetProvider(string providerName = null) {
            var provider = _cacheUtility.GetOutputCacheProvider(_context, providerName);

            // if the context did not provide a provider, then use the default internal output cache provider for everything
            return provider ?? inMemoryOutputCacheProvider;
        }

        private bool HasDependencyChanged(string depKey, string[] fileDeps, string kernelKey, string oceKey, string providerName) {
            if (depKey == null) {
                return false;
            }
            // deserialize the file dependencies
            var dep = new CacheDependency(fileDeps);
            var idStartIndex = OutputcacheKeyprefixDependencies.Length;
            var idLength = depKey.Length - idStartIndex;
            // have the file dependencies changed?
            if (string.Compare(dep.GetUniqueID(), 0, depKey, idStartIndex, idLength, StringComparison.OrdinalIgnoreCase) == 0) {
                // file dependencies have not changed--cache them with callback to remove OutputCacheEntry if they change
                var dce = new DependencyCacheEntry {
                    RawResponseKey = oceKey,
                    KernelCacheUrl = kernelKey,
                    ProviderName = providerName
                };
                memoryCache.Set(depKey, dce, GetCacheItemPolicy(dep));
                return false;
            }
            // file dependencies have changed
            dep.Dispose();
            return true;
        }

        private async Task RemoveAsync(string key) {
            var provider = GetProvider();
            await provider.RemoveAsync(key);
        }

        private async Task InsertResponseAsync(string cachedVaryKey,
            CachedVary cachedVary,
            string rawResponseKey,
            CachedRawResponse rawResponse,
            CacheDependency dependencies,
            DateTime absExp,
            TimeSpan slidingExpiration) {

            var provider = GetProvider();
            // CachedVary can be serialized.
            // CachedRawResponse is not always serializable.
            bool canUseProvider = (rawResponse.CachePolicy.IsValidationCallbackSerializable()
                                   && slidingExpiration == Cache.NoSlidingExpiration
                                   && (dependencies == null || dependencies.GetFileDependencies() != null));
            if (!canUseProvider) {
                throw new Exception(SR.Provider_does_not_support_policy_for_responses);
            }
            if (cachedVary != null) {
                /*
                 * Add the CachedVary item so that a request will know
                 * which headers are needed to issue another request.
                 * 
                 * Use the Add method so that we guarantee we only use
                 * a single CachedVary and don't overwrite existing ones.
                 */
                var cachedVaryInCache =
                    (CachedVary)await provider.AddAsync(cachedVaryKey, cachedVary, Cache.NoAbsoluteExpiration);

                if (cachedVaryInCache != null) {
                    if (!cachedVary.Equals(cachedVaryInCache)) {
                        await provider.SetAsync(cachedVaryKey, cachedVary, Cache.NoAbsoluteExpiration);
                    }
                    else {
                        cachedVary = cachedVaryInCache;
                    }
                }
                // not all caches support cache key dependencies, but we can use a "change number" to associate
                // the ControlCachedVary and the PartialCachingCacheEntry
                rawResponse.CachedVaryId = cachedVary.CachedVaryId;
            }
            // Now insert into the cache
            string depKey = null;
            OutputCacheEntry oce = null;
            if (dependencies != null) {
                depKey = OutputcacheKeyprefixDependencies + dependencies.GetUniqueID();
                oce = converter.CreateOutputCacheEntry(rawResponse, depKey, dependencies.GetFileDependencies());
            }
            else {
                oce = converter.CreateOutputCacheEntry(rawResponse, null, null);
            }
            await provider.SetAsync(rawResponseKey, oce, absExp);

            if (dependencies != null) {
                // Check if Cache Dependency is supported
                var cacheDepHandler = provider as ICacheDependencyHandler;
                if (cacheDepHandler != null) {
                    var dce = new DependencyCacheEntry {
                        RawResponseKey = rawResponseKey,
                        KernelCacheUrl = oce.KernelCacheUrl,
                        ProviderName = provider.Name
                    };
                    await cacheDepHandler.AddAsync(depKey, dce, GetCacheItemPolicy(dependencies));
                }
                dependencies.Dispose();
            }
        }

        private CacheItemPolicy GetCacheItemPolicy(CacheDependency dependency) {
            var cacheItemPolicy = new CacheItemPolicy();
            cacheItemPolicy.RemovedCallback = (new RemovedCallback(s_dependencyRemovedCallback)).CacheEntryRemovedCallback;
            var filePaths = new List<string>();

            foreach (string fileDependency in dependency.GetFileDependencies()) {
                filePaths.Add(fileDependency);
            }
            var fileChangeMonitor = new HostFileChangeMonitor(filePaths);
            cacheItemPolicy.ChangeMonitors.Add(fileChangeMonitor);
            return cacheItemPolicy;
        }

        private bool IsCacheableEncoding(string headerContentEncodings, HttpCacheVaryByContentEncodings varyByContentEncodings) {
            // return true if we are not varying by content encoding.
            if (varyByContentEncodings == null) {
                return true;
            }
            // return true if there is no Content-Encoding header
            if (headerContentEncodings == null) {
                return true;
            }

            // return true if the Content-Encoding header is listed within varyByContentEncodings
            var headerContentEncodingCollection = headerContentEncodings.Split(new Char[] { ',' });
            foreach (var headerContentEncoding in headerContentEncodingCollection) {
                if (varyByContentEncodings.GetContentEncodings().Any(varyByContentEncoding => varyByContentEncoding.Equals(headerContentEncoding, StringComparison.OrdinalIgnoreCase))) {
                    return true;
                }
            }
            return false;
        }

        private bool ContainsNonShareableCookies() {
            var cookies = _context.Response.Cookies;
            for (int i = 0; i < cookies.Count; i++) {
                var httpCookie = cookies[i];
                if (httpCookie != null && !httpCookie.Shareable) {
                    return true;
                }
            }
            return false;
        }

        private void UseSnapshot(HttpRawResponse rawResponse, bool sendBody) {
            var response = _context.Response;
            if (response.HeadersWritten)
                throw new HttpException(SR.Cannot_use_snapshot_after_headers_sent);
            response.Clear();
            response.ClearHeaders();
            // restore status
            response.StatusCode = rawResponse.StatusCode;
            response.StatusDescription = rawResponse.StatusDescription;
            // restore headers
            foreach (var h in rawResponse.Headers.AllKeys) {
                response.Headers.Add(h, rawResponse.Headers[h]);
            }
            // restore content
            _cacheUtility.SetContentBuffers(_context, (ArrayList)rawResponse.Buffers);
            response.SuppressContent = !sendBody;
        }

        private HttpRawResponse GetSnapshot() {
            var headers = new NameValueCollection();
            var response = _context.Response;
            if (response.HeadersWritten)
                throw new HttpException(SR.Cannot_get_snapshot_if_not_buffered);
            foreach (var h in response.Headers.AllKeys) {
                if
                    (h.Equals(HttpHeaders.Server, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(HttpHeaders.SetCookie, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(HttpHeaders.CacheControl, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(HttpHeaders.Expires, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(HttpHeaders.LastModified, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(HttpHeaders.Etag, StringComparison.OrdinalIgnoreCase) ||
                     h.Equals(HttpHeaders.Vary, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                headers.Add(h, response.Headers[h]);
            }
            return new HttpRawResponse {
                StatusCode = response.StatusCode,
                StatusDescription = response.StatusDescription,
                Headers = headers,
                Buffers = _cacheUtility.GetContentBuffers(_context),
            };
        }

        private HttpCachePolicySettings GetCurrentSettings() {
            //update some headers fields within the response.cache object
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);
            return new HttpCachePolicySettings {
                Cacheability = cache.GetCacheability(),
                ValidationCallbackInfo = _cacheUtility.GetValidationCallbacks(_context),
                VaryByContentEncodings = cache.VaryByContentEncodings.GetContentEncodings(),
                VaryByHeaders = cache.VaryByHeaders.GetHeaders(),
                VaryByParams = cache.VaryByParams.GetParams(),
                VaryByCustom = cache.GetVaryByCustom(),
                UtcExpires = cache.GetExpires(),
                MaxAge = cache.GetMaxAge(),
                SlidingExpiration = cache.HasSlidingExpiration(),
                IgnoreRangeRequests = cache.GetIgnoreRangeRequests(),
                UtcLastModified = cache.GetUtcLastModified(),
                ETag = cache.GetETag(),
                GenerateLastModifiedFromFiles = cache.GetLastModifiedFromFileDependencies(),
                GenerateEtagFromFiles = cache.GetETagFromFileDependencies(),
                UtcTimestampCreated = cache.UtcTimestampCreated
            };
        }

        private void ResetFromHttpCachePolicySettings(HttpCachePolicySettings settings, DateTime utcTimestampRequest) {
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);

            cache.SetCacheability(settings.Cacheability);
            cache.VaryByContentEncodings.SetContentEncodings(settings.VaryByContentEncodings);
            cache.VaryByHeaders.SetHeaders(settings.VaryByHeaders);
            cache.VaryByParams.SetParams(settings.VaryByParams);
            if (settings.VaryByCustom != null) {
                cache.SetVaryByCustom(settings.VaryByCustom);
            }
            cache.SetExpires(settings.UtcExpires);
            cache.SetMaxAge(settings.MaxAge);
            cache.SetSlidingExpiration(settings.SlidingExpiration);
            cache.UtcTimestampCreated = settings.UtcTimestampCreated;
            cache.SetValidUntilExpires(settings.ValidUntilExpires);
            cache.SetLastModified(settings.UtcLastModified);
            if (settings.ETag != null) {
                cache.SetETag(settings.ETag);
            }
            if (settings.GenerateLastModifiedFromFiles) {
                cache.SetLastModifiedFromFileDependencies();
            }
            if (settings.GenerateEtagFromFiles) {
                cache.SetETagFromFileDependencies();
            }
            if (settings.ValidationCallbackInfo == null) {
                return;
            }
            foreach (var vci in settings.ValidationCallbackInfo) {
                cache.AddValidationCallback(vci.Key, vci.Value);
            }
        }

        private void UpdateCachedHeaders() {
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);
            //To enable Out of Band OutputCache Module support, we will always refresh the UtcTimestampRequest.
            if (cache.UtcTimestampCreated == DateTime.MinValue) {
                cache.UtcTimestampCreated = _context.Timestamp.ToUniversalTime();
            }
            UpdateFromDependencies();
        }

        private void UpdateFromDependencies() {
            CacheDependency dep = null;
            DateTime utcFileLastModifiedMax;
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);
            // if response.Cache.GetETag() != null && response.Cache.GetETagFromFileDependencies() == true, then this HttpCachePolicy
            // was created from HttpCachePolicySettings and we don't need to update _etag.
            if (cache.GetETag() == null && cache.GetETagFromFileDependencies()) {
                dep = _cacheUtility.CreateCacheDependency(_context);
                if (dep == null) {
                    return;
                }
                var id = dep.GetUniqueID();
                if (id == null) {
                    throw new HttpException(SR.No_UniqueId_Cache_Dependency);
                }
                utcFileLastModifiedMax = UpdateLastModifiedTimeFromDependency(dep);
                var sb = new StringBuilder(256);
                sb.Append(HttpRuntime.AppDomainId);
                sb.Append(id);
                sb.Append("+LM");
                sb.Append(utcFileLastModifiedMax.Ticks.ToString(CultureInfo.InvariantCulture));
                cache.SetETag("\"" +
                                       System.Convert.ToBase64String(
                                           CryptoUtil.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))) + "\"");
                if (!cache.GetLastModifiedFromFileDependencies())
                    return;
            }
            if (dep == null) {
                dep = _cacheUtility.CreateCacheDependency(_context);
                if (dep == null) {
                    return;
                }
            }
            utcFileLastModifiedMax = UpdateLastModifiedTimeFromDependency(dep);
            UtcSetLastModified(utcFileLastModifiedMax);
        }

        private void UtcSetLastModified(DateTime utcDate) {
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);
            /*
             * Time may differ if the system time changes in the middle of the request. 
             * Adjust the timestamp to Now if necessary.
             */
            var utcNow = DateTime.UtcNow;
            if (utcDate > utcNow) {
                utcDate = utcNow;
            }

            /*
             * Because HTTP dates have a resolution of 1 second, we
             * need to store dates with 1 second resolution or comparisons
             * will be off.
             */
            utcDate = new DateTime(utcDate.Ticks - (utcDate.Ticks % TimeSpan.TicksPerSecond));
            if (cache.GetUtcLastModified() != DateTime.MinValue || utcDate > cache.GetUtcLastModified()) {
                cache.SetLastModified(utcDate);
            }
        }

        private DateTime UpdateLastModifiedTimeFromDependency(CacheDependency dep) {
            var utcFileLastModifiedMax = dep.UtcLastModified;
            var cache = _cacheUtility.GetCachePolicyFromHttpContextBase(_context);
            if (utcFileLastModifiedMax < cache.GetUtcLastModified()) {
                utcFileLastModifiedMax = cache.GetUtcLastModified();
            }
            // account for difference between file system time 
            // and DateTime.Now. On some machines it appears that
            // the last modified time is further in the future
            // that DateTime.Now                
            var utcNow = DateTime.UtcNow;
            if (utcFileLastModifiedMax > utcNow) {
                utcFileLastModifiedMax = utcNow;
            }
            return utcFileLastModifiedMax;
        }

        private string CreateOutputCachedItemKey(string path, string verb, CachedVary cachedVary) {
            var request = _context.Request;
            var sb = verb.Equals(HttpMethods.POST, StringComparison.OrdinalIgnoreCase)
                ? new StringBuilder(OutputcacheKeyprefixPost, path.Length + OutputcacheKeyprefixPost.Length)
                : new StringBuilder(OutputcacheKeyprefixGet, path.Length + OutputcacheKeyprefixGet.Length);
            sb.Append(CultureInfo.InvariantCulture.TextInfo.ToLower(path));
            /* key for cached vary item has additional information */
            if (cachedVary == null) {
                return sb.ToString();
            }
            /* params part */
            int j;
            string value;
            for (j = 0; j <= 2; j++) {
                string[] a = null;
                NameValueCollection col = null;
                bool getAllParams = false;
                switch (j) {
                    case 0:
                        sb.Append("H");
                        a = cachedVary.Headers;
                        if (a != null) {
                            col = request.ServerVariables;
                        }
                        break;
                    case 1:
                        sb.Append("Q");
                        a = cachedVary.Params;
                        if ((a != null || cachedVary.VaryByAllParams)) {
                            col = request.QueryString;
                            getAllParams = cachedVary.VaryByAllParams;
                        }
                        break;
                    default:
                        sb.Append("F");
                        if (verb.Equals(HttpMethods.POST, StringComparison.OrdinalIgnoreCase)) {
                            a = cachedVary.Params;
                            if ((a != null || cachedVary.VaryByAllParams)) {
                                col = request.Form;
                                getAllParams = cachedVary.VaryByAllParams;
                            }
                        }
                        break;
                }
                /* handle all params case (VaryByParams[*] = true) */
                int i;
                if (getAllParams && col.Count > 0) {
                    a = col.AllKeys;
                    for (i = a.Length - 1; i >= 0; i--) {
                        if (a[i] != null)
                            a[i] = CultureInfo.InvariantCulture.TextInfo.ToLower(a[i]);
                    }
                    Array.Sort(a, InvariantComparer.Default);
                }
                if (a == null) {
                    continue;
                }
                int n;
                for (i = 0, n = a.Length; i < n; i++) {
                    string name = a[i];
                    if (col == null) {
                        value = NullVarybyValue;
                    }
                    else {
                        value = col[name] ?? NullVarybyValue;
                    }
                    sb.Append("N");
                    sb.Append(name);
                    sb.Append("V");
                    sb.Append(value);
                }
            }
            /* custom string part */
            sb.Append("C");
            if (cachedVary.VaryByCustom != null) {
                sb.Append("N");
                sb.Append(cachedVary.VaryByCustom);
                sb.Append("V");
                value = _cacheUtility.GetVaryByCustomString(_context, cachedVary.VaryByCustom) ?? NullVarybyValue;
                sb.Append(value);
            }
            /* 
                 * if VaryByParms=*, and method is not a form, then 
                 * use a cryptographically strong hash of the data as
                 * part of the key.
                 */
            sb.Append("D");
            if (verb.Equals(HttpMethods.POST, StringComparison.OrdinalIgnoreCase) &&
                cachedVary.VaryByAllParams &&
                request.Form.Count == 0) {

                int contentLength = request.ContentLength;
                if (contentLength > MaxPostKeyLength || contentLength < 0) {
                    return null;
                }
                if (contentLength > 0) {
                    using (var ms = new MemoryStream()) {
                        request.InputStream.CopyTo(ms);
                        byte[] buf = ms.ToArray();
                        // Use SHA256 to generate a collision-free hash of the input data
                        value = Convert.ToBase64String((CryptoUtil.ComputeHash(buf)));
                        sb.Append(value);
                    }
                }
            }
            /*
            * VaryByContentEncoding
            */
            sb.Append("E");
            var contentEncodings = cachedVary.ContentEncodings;
            if (contentEncodings == null) {
                return sb.ToString();
            }
            if (_context.Request.Headers[HttpHeaders.AcceptEncoding] != null) {
                var headerContentEncodingCollection = _context.Request.Headers[HttpHeaders.AcceptEncoding].Split(new char[] { ',' });
                foreach (var headerContentEncoding in headerContentEncodingCollection) {
                    if (contentEncodings.Any(t => t.Equals(headerContentEncoding, StringComparison.OrdinalIgnoreCase))) {
                        sb.Append(headerContentEncoding);
                        break;
                    }
                }
            }
            // The key must end in "E", or the VaryByContentEncoding feature will break. Unfortunately, 
            // there was no good way to encapsulate the logic within this routine.  See the code in
            // OnEnter where we append the result of GetAcceptableEncoding to the key.
            return sb.ToString();
        }

        // Get the weight of the specified coding from the Accept-Encoding header.
        // 1 means use this coding.  0 means don't use this coding.  A number between
        // 1 and 0 must be compared with other codings.  -1 means the coding was not found
        private double GetAcceptableEncodingHelper(string coding, string acceptEncoding) {
            var weight = -1d;
            var startSearchIndex = 0;
            var codingLength = coding.Length;
            var acceptEncodingLength = acceptEncoding.Length;
            var maxSearchIndex = acceptEncodingLength - codingLength;

            while (startSearchIndex < maxSearchIndex) {
                var indexStart = acceptEncoding.IndexOf(coding, startSearchIndex, StringComparison.OrdinalIgnoreCase);

                if (indexStart == -1) {
                    break; // not found
                }

                // if index is in middle of string, previous char should be ' ' or ','
                if (indexStart != 0) {
                    var previousChar = acceptEncoding[indexStart - 1];
                    if (previousChar != ' ' && previousChar != ',') {
                        startSearchIndex = indexStart + 1;
                        continue; // move index forward and continue searching
                    }
                }

                // the match starts on a token boundary, but it must also end
                // on a token boundary ...

                var indexNextChar = indexStart + codingLength;
                var nextChar = '\0';
                if (indexNextChar < acceptEncodingLength) {
                    nextChar = acceptEncoding[indexNextChar];
                    while (nextChar == ' ' && ++indexNextChar < acceptEncodingLength) {
                        nextChar = acceptEncoding[indexNextChar];
                    }
                    if (nextChar != ' ' && nextChar != ',' && nextChar != ';') {
                        startSearchIndex = indexStart + 1;
                        continue; // move index forward and continue searching
                    }
                }
                weight = (nextChar == ';') ? ParseWeight(acceptEncoding, indexNextChar) : 1;
                break; // found
            }
            return weight;
        }

        // Gets the weight of the encoding beginning at startIndex.
        // If Accept-Encoding header is formatted incorrectly, return 1 to short-circuit search.
        private double ParseWeight(string acceptEncoding, int startIndex) {
            var weight = 1d;
            var tokenEnd = acceptEncoding.IndexOf(',', startIndex);
            if (tokenEnd == -1) {
                tokenEnd = acceptEncoding.Length;
            }
            var qIndex = acceptEncoding.IndexOf('q', startIndex);
            if (qIndex <= -1 || qIndex >= tokenEnd) {
                return weight;
            }
            var equalsIndex = acceptEncoding.IndexOf('=', qIndex);
            if (equalsIndex <= -1 || equalsIndex >= tokenEnd) {
                return weight;
            }
            var s = acceptEncoding.Substring(equalsIndex + 1, tokenEnd - (equalsIndex + 1));
            double d;
            if (double.TryParse(s, NumberStyles.Float & ~NumberStyles.AllowLeadingSign & ~NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out d)) {
                weight = (d >= 0 && d <= 1) ? d : 1;
                // if format is invalid, short-circut search by returning weight of 1
            }
            return weight;
        }

        private bool IsIdentityAcceptable(string acceptEncoding) {
            var identityWeight = GetAcceptableEncodingHelper(Identity, acceptEncoding);
            if (identityWeight == 0
                || (identityWeight <= 0 && GetAcceptableEncodingHelper(Asterisk, acceptEncoding) == 0)) {
                return false;
            }
            return true;
        }

        private async Task InsertResponseAsync(string key, DateTime utcExpires, CachedVary cachedVary, HttpCachePolicySettings settings, string keyRawResponse, TimeSpan slidingDelta) {
            if (utcExpires > DateTime.Now) {
                // Create the response object to be sent on cache hits.
                var httpRawResponse = GetSnapshot();
                string kernelCacheUrl = null;
                //Insert the response into kernel Cache
                if (IsKernelCacheAPISupported()) {
                    kernelCacheUrl = _cacheUtility.SetupKernelCaching(null, _context);
                }
                var cachedVaryId = cachedVary?.CachedVaryId ?? Guid.Empty;
                var cachedRawResponse = new CachedRawResponse {
                    RawResponse = httpRawResponse,
                    CachePolicy = settings,
                    KernelCacheUrl = kernelCacheUrl,
                    CachedVaryId = cachedVaryId
                };
                using (var dep = _cacheUtility.CreateCacheDependency(_context)) {
                    await InsertResponseAsync(key, cachedVary,
                        keyRawResponse, cachedRawResponse,
                        dep,
                        utcExpires, slidingDelta);
                }
            }
        }

        private bool CheckIfNoneMatch(HttpCachePolicySettings settings) {
            /* Check "If-None-Match" header */
            var etagHeader = _context.Request.Headers[HttpHeaders.IfNoneMatch];
            if (etagHeader != null) {
                var etags = etagHeader.Split(s_fieldSeparators);
                for (int i = 0, n = etags.Length; i < n; i++) {
                    if (i == 0 && etags[i].Equals(Asterisk)) {
                        return true;
                    }
                    if (etags[i].Equals(settings.ETag)) {
                        return true;
                    }
                }
                return false;
            }
            return true;
        }

        private bool CheckIfModifiedSince(HttpCachePolicySettings settings) {
            var ifModifiedSinceHeader = _context.Request.Headers[HttpHeaders.IfModifiedSince];
            if (ifModifiedSinceHeader != null && settings.UtcLastModified != DateTime.MinValue &&
                    settings.UtcLastModified <= HttpDate.UtcParse(ifModifiedSinceHeader) &&
                    HttpDate.UtcParse(ifModifiedSinceHeader) <= _context.Timestamp.ToUniversalTime()) {
                return true;
            }
            return false;
        }

        internal bool CheckMaxAge(string directive, HttpCachePolicySettings settings) {
            if (directive.Equals(CacheDirectives.NoCache, StringComparison.OrdinalIgnoreCase) || directive.Equals(CacheDirectives.NoStore, StringComparison.OrdinalIgnoreCase)) {
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
                        ((_context.Timestamp.Ticks - settings.UtcTimestampCreated.Ticks) /
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
                (int)((settings.UtcExpires.Ticks - _context.Timestamp.Ticks) / TimeSpan.TicksPerSecond);
            if (fresh >= minfresh) {
                return false;
            }
            return true;
        }
        #endregion     
    }
}