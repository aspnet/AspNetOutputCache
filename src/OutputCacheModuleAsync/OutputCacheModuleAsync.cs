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
    using System.Configuration;

    /// <summary>
    /// OutputCache Async Module, this Module is able to use Async type of OutputCache Providers 
    /// </summary>
    public class OutputCacheModuleAsync : IHttpModule {
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
            if (!OutputCacheHelper.IsHttpMethodSupported(request)) {
                return;
            }

            // Create a lookup key. Also store the key in global parameter _key to be used inside OnLeave() later   
            string key = OutputCacheHelper.CreateOutputCachedItemKey(context, null);

            // Lookup the cache vary using the key
            object item = await OutputCacheHelper.GetItemAsync(key);
            if (item == null) {
                return;
            }
            // 'item' may be one of the following:
            //  - a CachedVary object (if the object varies by something)
            //  - a CachedRawResponse object (i.e. it doesn't vary on anything)
            //  First assume it's a CacheVary
            var cachedVary = item as CachedVary;
            object cachedItem = null;
            if (cachedVary != null) {
                cachedItem = await OutputCacheHelper.GetAsCacheVaryAsync(cachedVary, context);
                if (cachedItem == null)
                    return;
            }

            // From this point on, we have an Raw Response entry to work with.
            var cachedRawResponse = (CachedRawResponse)cachedItem;
            HttpCachePolicySettings settings = cachedRawResponse.CachePolicy;
            if (OutputCacheHelper.CheckCachedVary(request, cachedVary, settings)) {
                return;
            }
            if (settings.IgnoreRangeRequests && OutputCacheHelper.IsRangeRequest(request)) {
                return;
            }

            if (OutputCacheHelper.CheckHeaders(settings, context)) {
                return;
            }
            if (await OutputCacheHelper.CheckValidityAsync(key, settings, context)) {
                return;
            }

            HttpRawResponse rawResponse = cachedRawResponse.RawResponse;
            if (!OutputCacheHelper.IsContentEncodingAcceptable(cachedVary, request, rawResponse)) {
                return;
            }
            OutputCacheHelper.UpdateCachedResponse(context, settings, rawResponse);

            // re-insert entry in kernel cache if necessary
            string originalCacheUrl = cachedRawResponse.KernelCacheUrl;
            if (originalCacheUrl != null) {
                OutputCacheUtility.SetupKernelCaching(originalCacheUrl, context.Response);
            }
            app.CompleteRequest();
        }

        private async Task OnLeaveAsync(object source, EventArgs eventArgs) {
            HttpContext context = ((HttpApplication)source).Context;
            //Determine whether the response is cacheable.
            if (!OutputCacheHelper.IsResponseCacheable(context)) {
                return;
            }
            await OutputCacheHelper.CacheResponseAsync(context);
        }
    }
}