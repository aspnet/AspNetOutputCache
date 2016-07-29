using System.IO;
using Microsoft.AspNet.OutputCache.Resource;

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
    using System.Diagnostics;

    internal class OutputCacheHelper {
        private const int MaxPostKeyLength = 15000;
        private const string NullVarybyValue = "+n+";
        internal const string TagOutputcache = "OutputCache";
        private const string OutputcacheKeyprefixPost = "a1";
        private const string OutputcacheKeyprefixGet = "a2";
        private const string Identity = "identity";
        private const string Asterisk = "*";
        private const string OutputcacheKeyprefixDependencies = "Microsoft.AspNet.OutputCache.Dependencies";
        private readonly CacheItemRemovedCallback _dependencyRemovedCallback = null;
        private InMemoryOutputCacheProvider _inMemoryOutputCacheProvider;

        private InMemoryOutputCacheProvider InMemoryOutputCacheProvider => _inMemoryOutputCacheProvider ??
                                                                           (_inMemoryOutputCacheProvider =
                                                                               new InMemoryOutputCacheProvider());

        private async Task RemoveFromProvider(string key, string providerName) {
            OutputCacheProviderAsync provider;
            // we know where it is.  If providerName is given,
            // then it is in that provider.  If it's not given,
            // it's in the internal cache.
            if (providerName != null) {
                OutputCacheProviderCollection providers = OutputCache.Providers;
                provider = (OutputCacheProviderAsync) providers?[providerName];
            }
            else {
                provider = InMemoryOutputCacheProvider;
            }
            if (provider != null) {
                await provider.RemoveAsync(key);
            }
        }

        private OutputCacheProviderAsync GetProvider(HttpContext context) {
            OutputCacheProviderAsync provider = null;
            string providerName = context.ApplicationInstance.GetOutputCacheProviderName(context);
            if (OutputCache.Providers != null) {
                provider = OutputCache.Providers[providerName] as OutputCacheProviderAsync;
            }
            // if the context did not provide a provider, then use the default internal output cache provider for everything
            return provider ?? InMemoryOutputCacheProvider;
        }

        private bool HasDependencyChanged(string depKey, string[] fileDeps, string kernelKey, string oceKey,
            string providerName) {
            if (depKey == null) {
                return false;
            }
            // is the file dependency already in the in-memory cache?
            if (InMemoryOutputCacheProvider.Get(depKey) != null) {
                return false;
            }
            // deserialize the file dependencies
            var dep = new CacheDependency(fileDeps);
            int idStartIndex = OutputcacheKeyprefixDependencies.Length;
            int idLength = depKey.Length - idStartIndex;
            CacheItemRemovedCallback callback = _dependencyRemovedCallback;
            // have the file dependencies changed?
            if (string.Compare(dep.GetUniqueID(), 0, depKey, idStartIndex, idLength, StringComparison.Ordinal) == 0) {
                // file dependencies have not changed--cache them with callback to remove OutputCacheEntry if they change
                var dce = new DependencyCacheEntry {
                    RawResponseKey = oceKey,
                    KernelCacheUrl = kernelKey,
                    Name = providerName
                };
                var dcew = new DependencyCacheEntryWrapper {
                    DependencyCacheEntry = dce,
                    Dependencies = dep,
                    CacheItemPriority = CacheItemPriority.Normal,
                    DependencyCacheTimeSpan = Cache.NoSlidingExpiration,
                    DependencyRemovedCallback = callback
                };
                InMemoryOutputCacheProvider.Add(depKey, dcew, DateTimeOffset.MaxValue);
                return false;
            }
            // file dependencies have changed
            dep.Dispose();
            return true;
        }

        private static OutputCacheEntry Convert(CachedRawResponse cachedRawResponse, string depKey,
            string[] fileDependencies) {
            NameValueCollection headers = cachedRawResponse.RawResponse.Headers;
            ArrayList responseElements = null;
            ArrayList buffers = cachedRawResponse.RawResponse.Buffers;
            int count = buffers?.Count ?? 0;
            for (int i = 0; i < count; i++) {
                if (responseElements == null) {
                    responseElements = new ArrayList(count);
                }
                // wrapper them as differnt type of responseelement then save into responseElement. why?
                if (buffers != null) responseElements.Add(buffers[i]);
            }
            var oce = new OutputCacheEntry() {
                CachedVaryId = cachedRawResponse.CachedVaryId,
                Settings = cachedRawResponse.CachePolicy,
                KernelCacheUrl = cachedRawResponse.KernelCacheUrl,
                DependenciesKey = depKey,
                Dependencies = fileDependencies,
                StatusCode = cachedRawResponse.RawResponse.StatusCode,
                StatusDescription = cachedRawResponse.RawResponse.StatusDescription,
                HeaderElements = headers,
                ResponseElements = responseElements
            };
            return oce;
        }

        private static CachedRawResponse Convert(OutputCacheEntry oce) {
            NameValueCollection headers = null;
            if (oce.HeaderElements != null && oce.HeaderElements.Count > 0) {
                headers = new NameValueCollection(oce.HeaderElements.Count);
                foreach (string h in oce.HeaderElements.AllKeys) {
                    headers.Add(h, oce.HeaderElements[h]);
                }
            }
            ArrayList buffers;
            if (oce.ResponseElements != null && oce.ResponseElements.Count > 0) {
                buffers = new ArrayList(oce.ResponseElements.Count);
                foreach (object bf in oce.ResponseElements) {
                    buffers.Add(bf);
                }
            }
            else {
                buffers = new ArrayList();
            }

            return new CachedRawResponse {
                RawResponse =
                    new HttpRawResponse {
                        StatusCode = oce.StatusCode,
                        StatusDescription = oce.StatusDescription,
                        Headers = headers,
                        Buffers = buffers,
                        HasSubstBlocks = false
                    },
                CachePolicy = oce.Settings,
                KernelCacheUrl = oce.KernelCacheUrl,
                CachedVaryId = oce.CachedVaryId
            };
        }

        internal async Task<object> GetAsync(string key) {
            OutputCacheProviderAsync provider = GetProvider(HttpContext.Current);
            object result = await provider.GetAsync(key);
            var oce = result as OutputCacheEntry;
            if (oce == null) {
                return result;
            }
            if (HasDependencyChanged(oce.DependenciesKey, oce.Dependencies, oce.KernelCacheUrl, key, provider.Name)) {
                await RemoveFromProvider(key, provider.Name);
                return null;
            }
            result = Convert(oce);
            return result;
        }

        internal async Task RemoveAsync(string key, HttpContext context) {
            // we don't know if it's in the internal cache or
            // one of the providers.  If a context is given,
            // then we can narrow down to at most one provider.
            // If the context is null, then we don't know which
            // provider and we have to check all.   
            if (context == null) {
                // remove from all providers since we don't know which one it's in.
                OutputCacheProviderCollection providers = OutputCache.Providers;
                foreach (OutputCacheProviderAsync provider in providers) {
                    await provider.RemoveAsync(key);
                }
            }
            else {
                OutputCacheProviderAsync provider = GetProvider(context);
                await provider.RemoveAsync(key);
            }
        }

        internal async Task InsertResponseAsync(string cachedVaryKey,
            CachedVary cachedVary,
            string rawResponseKey,
            CachedRawResponse rawResponse,
            CacheDependency dependencies,
            DateTime absExp,
            TimeSpan slidingExpiration) {
            // if the provider is undefined or the fragment can't be inserted in the
            // provider, insert it in the internal cache.
            OutputCacheProviderAsync provider = GetProvider(HttpContext.Current);
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
                    (CachedVary) await provider.AddAsync(cachedVaryKey, cachedVary, Cache.NoAbsoluteExpiration);

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
            // Now insert into the cache (use cache provider if possible, otherwise use internal cache)
            if (dependencies != null) {
                string depKey = OutputcacheKeyprefixDependencies + dependencies.GetUniqueID();
                string[] fileDeps = dependencies.GetFileDependencies();
                OutputCacheEntry oce = Convert(rawResponse, depKey, fileDeps);
                await provider.SetAsync(rawResponseKey, oce, absExp);
                {
                    // use Add and dispose dependencies if there's already one in the cache
                    var dce = new DependencyCacheEntry {
                        RawResponseKey = rawResponseKey,
                        KernelCacheUrl = oce.KernelCacheUrl,
                        Name = provider.Name
                    };
                    var dcew = new DependencyCacheEntryWrapper {
                        DependencyCacheEntry = dce,
                        Dependencies = dependencies,
                        CacheItemPriority = CacheItemPriority.Normal,
                        DependencyCacheTimeSpan = Cache.NoSlidingExpiration,
                        DependencyRemovedCallback = _dependencyRemovedCallback
                    };
                    object d = await provider.AddAsync(depKey, dcew, absExp);
                    if (d != null) {
                        dependencies.Dispose();
                    }
                }
            }
        }

        internal static bool IsCacheableEncoding(string coding, string[] contentEncodings) {
            // return true if we are not varying by content encoding.
            if (contentEncodings == null) {
                return true;
            }
            // return true if there is no Content-Encoding header
            return coding == null || contentEncodings.Any(contentEncoding => contentEncoding == coding);
            // return true if the Content-Encoding header is listed
        }

        internal static bool ContainsNonShareableCookies(HttpResponse response) {
            HttpCookieCollection cookies = response.Cookies;
            for (int i = 0; i < cookies.Count; i++) {
                HttpCookie httpCookie = cookies[i];
                if (httpCookie != null && !httpCookie.Shareable) {
                    return true;
                }
            }
            return false;
        }

        internal static void UseSnapshot(HttpRawResponse rawResponse, bool sendBody, HttpResponse response) {
            if (response.HeadersWritten)
                throw new HttpException(SR.Cannot_use_snapshot_after_headers_sent);
            response.Clear();
            response.ClearHeaders();
            // restore status
            response.StatusCode = rawResponse.StatusCode;
            response.StatusDescription = rawResponse.StatusDescription;
            // restore headers
            foreach (string h in rawResponse.Headers.AllKeys) {
                response.Headers.Add(h, rawResponse.Headers[h]);
            }
            // restore content
            OutputCacheUtility.SetContentBuffers(response, rawResponse.Buffers);
            response.SuppressContent = !sendBody;
        }

        internal static HttpRawResponse GetSnapshot(HttpResponse response) {
            var headers = new NameValueCollection();
            const bool hasSubstBlocks = false;
            if (response.HeadersWritten)
                throw new HttpException(SR.Cannot_get_snapshot_if_not_buffered);
            // data
            ArrayList buffers = OutputCacheUtility.GetContentBuffers(response);
            // headers (after data as the data has side effects (like charset, see ASURT 113202))    
            int statusCode = response.StatusCode;
            string statusDescription = response.StatusDescription;
            foreach (string h in response.Headers.AllKeys) {
                if
                    (h == "HttpWorkerRequest.HeaderServer" ||
                     h == "HttpWorkerRequest.HeaderSetCookie" ||
                     h == "HttpWorkerRequest.HeaderCacheControl" ||
                     h == "HttpWorkerRequest.HeaderExpires" ||
                     h == "HttpWorkerRequest.HeaderLastModified" ||
                     h == "HttpWorkerRequest.HeaderEtag" ||
                     h == "HttpWorkerRequest.HeaderVary") {
                    continue;
                }
                headers.Add(h, response.Headers[h]);
            }
            return new HttpRawResponse {
                StatusCode = statusCode,
                StatusDescription = statusDescription,
                Headers = headers,
                Buffers = buffers,
                HasSubstBlocks = hasSubstBlocks
            };
        }

        internal static HttpCachePolicySettings GetCurrentSettings(HttpResponse response) {
            IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> validationCallbackInfo =
                OutputCacheUtility.GetValidationCallbacks(response);

            //update some headers fields within the response.cache object
            return new HttpCachePolicySettings {
                Cacheability = response.Cache.GetCacheability(),
                ValidationCallbackInfo = validationCallbackInfo,
                VaryByContentEncodings = response.Cache.VaryByContentEncodings.GetContentEncodings(),
                VaryByHeaders = response.Cache.VaryByHeaders.GetHeaders(),
                VaryByParams = response.Cache.VaryByParams.GetParams(),
                VaryByCustom = response.Cache.GetVaryByCustom(),
                UtcExpires = response.Cache.GetExpires(),
                MaxAge = response.Cache.GetMaxAge(),
                SlidingExpiration = response.Cache.HasSlidingExpiration(),
                IgnoreRangeRequests = response.Cache.GetIgnoreRangeRequests(),
                ValidUntilExpires = response.Cache.IsValidUntilExpires(),
                UtcLastModified = response.Cache.GetUtcLastModified(),
                ETag = response.Cache.GetETag(),
                GenerateLastModifiedFromFiles = response.Cache.GetLastModifiedFromFileDependencies(),
                GenerateEtagFromFiles = response.Cache.GetETagFromFileDependencies(),
                UtcTimestampCreated = response.Cache.UtcTimestampCreated
            };
        }

        internal static void ResetFromHttpCachePolicySettings(HttpCachePolicySettings settings,
            DateTime utcTimestampRequest, HttpResponse response) {
            response.Cache.SetCacheability(settings.Cacheability);
            response.Cache.VaryByContentEncodings.SetContentEncodings(settings.VaryByContentEncodings);
            response.Cache.VaryByHeaders.SetHeaders(settings.VaryByHeaders);
            response.Cache.VaryByParams.SetParams(settings.VaryByParams);
            if (settings.VaryByCustom != null) {
                response.Cache.SetVaryByCustom(settings.VaryByCustom);
            }
            response.Cache.SetExpires(settings.UtcExpires);
            response.Cache.SetMaxAge(settings.MaxAge);
            response.Cache.SetSlidingExpiration(settings.SlidingExpiration);
            response.Cache.UtcTimestampCreated = settings.UtcTimestampCreated;
            response.Cache.SetValidUntilExpires(settings.ValidUntilExpires);
            response.Cache.SetLastModified(settings.UtcLastModified);
            if (settings.ETag != null) {
                response.Cache.SetETag(settings.ETag);
            }
            if (settings.GenerateLastModifiedFromFiles) {
                response.Cache.SetLastModifiedFromFileDependencies();
            }
            if (settings.GenerateEtagFromFiles) {
                response.Cache.SetETagFromFileDependencies();
            }
            if (settings.ValidationCallbackInfo == null) {
                return;
            }
            foreach (KeyValuePair<HttpCacheValidateHandler, object> vci in settings.ValidationCallbackInfo) {
                response.Cache.AddValidationCallback(vci.Key, vci.Value);
            }
        }

        internal static void UpdateCachedHeaders(HttpResponse response) {
            //To enable Out of Band OutputCache Module support, we will always refresh the UtcTimestampRequest.
            if (response.Cache.UtcTimestampCreated == DateTime.MinValue) {
                response.Cache.UtcTimestampCreated = HttpContext.Current.Timestamp.ToUniversalTime();
            }         
            UpdateFromDependencies(response);
        }

        private static void UpdateFromDependencies(HttpResponse response) {
            CacheDependency dep = null;
            // if response.Cache.GetETag() != null && response.Cache.GetETagFromFileDependencies() == true, then this HttpCachePolicy
            // was created from HttpCachePolicySettings and we don't need to update _etag.
            if (response.Cache.GetETag() == null && response.Cache.GetETagFromFileDependencies()) {
                dep = OutputCacheUtility.CreateCacheDependency(response);
                if (dep == null) {
                    return;
                }
                string id = dep.GetUniqueID();
                if (id == null) {
                    throw new HttpException(SR.No_UniqueId_Cache_Dependency);
                }
                DateTime utcFileLastModifiedMax = UpdateLastModifiedTimeFromDependency(dep, response);
                var sb = new StringBuilder(256);
                sb.Append(HttpRuntime.AppDomainId);
                sb.Append(id);
                sb.Append("+LM");
                sb.Append(utcFileLastModifiedMax.Ticks.ToString(CultureInfo.InvariantCulture));
                response.Cache.SetETag("\"" +
                                       System.Convert.ToBase64String(
                                           CryptoUtil.ComputeSha256Hash(Encoding.UTF8.GetBytes(sb.ToString()))) + "\"");


                if (!response.Cache.GetLastModifiedFromFileDependencies())
                    return;
            }

            {
                if (dep == null) {
                    dep = OutputCacheUtility.CreateCacheDependency(response);
                    if (dep == null) {
                        return;
                    }
                }
                DateTime utcFileLastModifiedMax = UpdateLastModifiedTimeFromDependency(dep,response);
                UtcSetLastModified(utcFileLastModifiedMax, response);
            }
        }

        private static void UtcSetLastModified(DateTime utcDate, HttpResponse response) {
        
                /*
                 * Time may differ if the system time changes in the middle of the request. 
                 * Adjust the timestamp to Now if necessary.
                 */

                DateTime utcNow = DateTime.UtcNow;
                if (utcDate > utcNow) {
                    utcDate = utcNow;
                }

                /*
                 * Because HTTP dates have a resolution of 1 second, we
                 * need to store dates with 1 second resolution or comparisons
                 * will be off.
                 */

                utcDate = new DateTime(utcDate.Ticks - (utcDate.Ticks % TimeSpan.TicksPerSecond));
                if (response.Cache.GetUtcLastModified()!= DateTime.MinValue || utcDate > response.Cache.GetUtcLastModified()) {
                 response.Cache.SetLastModified(utcDate);
                }
            

        }


        private static DateTime UpdateLastModifiedTimeFromDependency(CacheDependency dep, HttpResponse response) {
                  DateTime utcFileLastModifiedMax = dep.UtcLastModified;
                if (utcFileLastModifiedMax <  response.Cache.GetUtcLastModified()) {
                    utcFileLastModifiedMax = response.Cache.GetUtcLastModified();
                }
                // account for difference between file system time 
                // and DateTime.Now. On some machines it appears that
                // the last modified time is further in the future
                // that DateTime.Now                
                DateTime utcNow = DateTime.UtcNow;
                if (utcFileLastModifiedMax > utcNow) {
                    utcFileLastModifiedMax = utcNow;
                }
                return utcFileLastModifiedMax;
            
        }

        private static string CreateOutputCachedItemKey(
            string path,
            string verb,
            HttpContext context,
            CachedVary cachedVary) {
            StringBuilder sb = verb == "POST"
                ? new StringBuilder(OutputcacheKeyprefixPost, path.Length + OutputcacheKeyprefixPost.Length)
                : new StringBuilder(OutputcacheKeyprefixGet, path.Length + OutputcacheKeyprefixGet.Length);
            sb.Append(CultureInfo.InvariantCulture.TextInfo.ToLower(path));
            /* key for cached vary item has additional information */
            if (cachedVary == null) {
                return sb.ToString();
            }
            HttpRequest request = context.Request;
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
                        Debug.Assert(cachedVary.Params == null || !cachedVary.VaryByAllParams,
                            "cachedVary._params == null || !cachedVary._varyByAllParams");
                        sb.Append("Q");
                        a = cachedVary.Params;
                        if ((a != null || cachedVary.VaryByAllParams)) {
                            col = request.QueryString;
                            getAllParams = cachedVary.VaryByAllParams;
                        }
                        break;
                    default:
                        Debug.Assert(cachedVary.Params == null || !cachedVary.VaryByAllParams,
                            "cachedVary._params == null || !cachedVary._varyByAllParams");
                        sb.Append("F");
                        if (verb == "POST") {
                            a = cachedVary.Params;
                            if ((a != null || cachedVary.VaryByAllParams)) {
                                col = request.Form;
                                getAllParams = cachedVary.VaryByAllParams;
                            }
                        }
                        break;
                }
                Debug.Assert(a == null || !getAllParams, "a == null || !getAllParams");
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
                value = context.ApplicationInstance.GetVaryByCustomString(
                    context, cachedVary.VaryByCustom) ?? NullVarybyValue;
                sb.Append(value);
            }
            /* 
                 * if VaryByParms=*, and method is not a form, then 
                 * use a cryptographically strong hash of the data as
                 * part of the key.
                 */
            sb.Append("D");
            if (verb == "POST" &&
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
                        value = System.Convert.ToBase64String((CryptoUtil.ComputeSha256Hash(buf)));
                        sb.Append(value);
                    }
                }
            }
            /*
            * VaryByContentEncoding
            */
            sb.Append("E");
            string[] contentEncodings = cachedVary.ContentEncodings;
            if (contentEncodings == null) {
                return sb.ToString();
            }
            string coding = context.Response.HeaderEncoding.ToString();
            if (contentEncodings.Any(t => t == coding)) {
                sb.Append(coding);
            }
            // The key must end in "E", or the VaryByContentEncoding feature will break. Unfortunately, 
            // there was no good way to encapsulate the logic within this routine.  See the code in
            // OnEnter where we append the result of GetAcceptableEncoding to the key.
            return sb.ToString();
        }

        /*
         * Return a key to lookup a cached response. The key contains 
         * the path and optionally, vary parameters, vary headers, custom strings,
         * and form posted data.
         */

        internal static string CreateOutputCachedItemKey(HttpContext context, CachedVary cachedVary) {
            return CreateOutputCachedItemKey(context.Request.Path, context.Request.HttpMethod, context, cachedVary);
        }

        /*
         * GetAcceptableEncoding finds an acceptable coding for the given
         * Accept-Encoding header (see RFC 2616)
         * returns either i) an acceptable index in contentEncodings, ii) -1 if the identity is acceptable, or iii) -2 if nothing is acceptable
         */

        internal static int GetAcceptableEncoding(string[] contentEncodings, int startIndex, string acceptEncoding) {
            // The format of Accept-Encoding is ( 1#( codings [ ";" "q" "=" qvalue ] ) | "*" )
            if (string.IsNullOrEmpty(acceptEncoding)) {
                return -1; // use "identity"
            }

            // is there only one token?
            int tokenEnd = acceptEncoding.IndexOf(',');
            if (tokenEnd == -1) {
                string acceptEncodingWithoutWeight = acceptEncoding;
                // WOS 1984913: is there a weight?
                tokenEnd = acceptEncoding.IndexOf(';');
                if (tokenEnd > -1) {
                    // remove weight
                    int space = acceptEncoding.IndexOf(' ');
                    if (space > -1 && space < tokenEnd) {
                        tokenEnd = space;
                    }
                    acceptEncodingWithoutWeight = acceptEncoding.Substring(0, tokenEnd);
                    if (Math.Abs(ParseWeight(acceptEncoding, tokenEnd)) < 0) {
                        //weight is 0, use "identity" only if it is acceptable
                        bool identityIsAcceptable = acceptEncodingWithoutWeight != Identity &&
                                                    acceptEncodingWithoutWeight != Asterisk;
                        return (identityIsAcceptable) ? -1 : -2;
                    }
                }
                // WOS 1985353: is this the special "*" symbol?
                if (acceptEncodingWithoutWeight == Asterisk) {
                    // just return the index of the first entry in the list, since it is acceptable
                    return 0;
                }
                for (int i = startIndex; i < contentEncodings.Length; i++) {
                    if (string.Equals(contentEncodings[i], acceptEncodingWithoutWeight,
                        StringComparison.OrdinalIgnoreCase)) {
                        return i; // found
                    }
                }
                return -1; // not found, use "identity"
            }
            // there are multiple tokens
            int bestCodingIndex = -1;
            double bestCodingWeight = 0;
            for (int i = startIndex; i < contentEncodings.Length; i++) {
                string coding = contentEncodings[i];
                // get weight of current coding
                double weight = GetAcceptableEncodingHelper(coding, acceptEncoding);
                // if it is 1, use it
                if (Math.Abs(weight - 1) < Tolerance) {
                    return i;
                }
                // if it is the best so far, remember it
                if (!(weight > bestCodingWeight)) {
                    continue;
                }
                bestCodingIndex = i;
                bestCodingWeight = weight;
            }
            // WOS 1985352: use "identity" only if it is acceptable
            if (bestCodingIndex == -1 && !IsIdentityAcceptable(acceptEncoding)) {
                bestCodingIndex = -2;
            }
            return bestCodingIndex; // coding index with highest weight, possibly -1 or -2
        }

        private static double Tolerance { get; set; }

        // GetAsync the weight of the specified coding from the Accept-Encoding header.
        // 1 means use this coding.  0 means don't use this coding.  A number between
        // 1 and 0 must be compared with other codings.  -1 means the coding was not found
        private static double GetAcceptableEncodingHelper(string coding, string acceptEncoding) {
            double weight = -1;
            int startSearchIndex = 0;
            int codingLength = coding.Length;
            int acceptEncodingLength = acceptEncoding.Length;
            int maxSearchIndex = acceptEncodingLength - codingLength;
            while (startSearchIndex < maxSearchIndex) {
                int indexStart = acceptEncoding.IndexOf(coding, startSearchIndex, StringComparison.OrdinalIgnoreCase);

                if (indexStart == -1) {
                    break; // not found
                }

                // if index is in middle of string, previous char should be ' ' or ','
                if (indexStart != 0) {
                    char previousChar = acceptEncoding[indexStart - 1];
                    if (previousChar != ' ' && previousChar != ',') {
                        startSearchIndex = indexStart + 1;
                        continue; // move index forward and continue searching
                    }
                }

                // the match starts on a token boundary, but it must also end
                // on a token boundary ...

                int indexNextChar = indexStart + codingLength;
                char nextChar = '\0';
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
        private static double ParseWeight(string acceptEncoding, int startIndex) {
            double weight = 1;
            int tokenEnd = acceptEncoding.IndexOf(',', startIndex);
            if (tokenEnd == -1) {
                tokenEnd = acceptEncoding.Length;
            }
            int qIndex = acceptEncoding.IndexOf('q', startIndex);
            if (qIndex <= -1 || qIndex >= tokenEnd) {
                return weight;
            }
            int equalsIndex = acceptEncoding.IndexOf('=', qIndex);
            if (equalsIndex <= -1 || equalsIndex >= tokenEnd) {
                return weight;
            }
            string s = acceptEncoding.Substring(equalsIndex + 1, tokenEnd - (equalsIndex + 1));
            double d;
            if (double.TryParse(s, NumberStyles.Float & ~NumberStyles.AllowLeadingSign & ~NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out d)) {
                weight = (d >= 0 && d <= 1) ? d : 1;
                // if format is invalid, short-circut search by returning weight of 1
            }
            return weight;
        }

        private static bool IsIdentityAcceptable(string acceptEncoding) {
            bool result = true;
            double identityWeight = GetAcceptableEncodingHelper(Identity, acceptEncoding);
            const double tolerance = 0;
            if (Math.Abs(identityWeight) < tolerance
                || (identityWeight <= 0 && Math.Abs(GetAcceptableEncodingHelper(Asterisk, acceptEncoding)) < 0)) {
                result = false;
            }
            return result;
        }

        internal static bool IsAcceptableEncoding(string contentEncoding, string acceptEncoding) {
            if (string.IsNullOrEmpty(contentEncoding)) {
                // if Content-Encoding is not set treat it as the identity
                contentEncoding = Identity;
            }
            if (string.IsNullOrEmpty(acceptEncoding)) {
                // only the identity is acceptable if Accept-Encoding is not set
                return (contentEncoding == Identity);
            }
            double weight = GetAcceptableEncodingHelper(contentEncoding, acceptEncoding);
            const double tolerance = 0;
            return !(Math.Abs(weight) < tolerance) &&
                   (!(weight <= 0) || Math.Abs(GetAcceptableEncodingHelper(Asterisk, acceptEncoding)) > 0);
        }


    }
}