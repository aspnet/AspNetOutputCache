namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Caching;
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
            var helper = new OutputCacheHelper(new HttpContextWrapper(app.Context));
            if (!helper.IsHttpMethodSupported()) {
                return;
            }

            // Create a lookup key. Also store the key in global parameter _key to be used inside OnLeave() later   
            string key = helper.CreateOutputCachedItemKey(null);

            // Lookup the cache vary using the key
            object item = await helper.GetAsync(key);
            if (item == null) {
                return;
            }

            // 'item' may be one of the following:
            //  - a CachedVary object (if the object varies by something)
            //  - a CachedRawResponse object (i.e. it doesn't vary on anything)
            //  First assume it's a CacheVary and try to get the cachedItem with it
            CachedRawResponse cachedRawResponse = null;
            var cachedVary = item as CachedVary;
            if (cachedVary != null) {
                var cachedItem = await helper.GetAsCacheVaryAsync(cachedVary);
                if (cachedItem != null) {
                    cachedRawResponse = (CachedRawResponse)cachedItem;
                }
            }
            if(cachedRawResponse == null) {
                cachedRawResponse = item as CachedRawResponse;
            }
            if (cachedRawResponse == null) {
                return;
            }

            // From this point on, we have an Raw Response entry to work with.
            HttpCachePolicySettings settings = cachedRawResponse.CachePolicy;
            if (helper.CheckCachedVary(cachedVary, settings)) {
                return;
            }
            if (settings.IgnoreRangeRequests && helper.IsRangeRequest()) {
                return;
            }
            if (helper.CheckHeaders(settings)) {
                return;
            }
            if (await helper.CheckValidityAsync(key, settings)) {
                return;
            }
            if (!helper.IsContentEncodingAcceptable(cachedVary, cachedRawResponse.RawResponse)) {
                return;
            }
            helper.UpdateCachedResponse(settings, cachedRawResponse.RawResponse);

            //Re-insert entry in kernel cache if necessary
            if (helper.IsKernelCacheAPISupported() && cachedRawResponse.KernelCacheUrl != null) {
                OutputCacheUtility.SetupKernelCaching(cachedRawResponse.KernelCacheUrl, app.Context.Response);
            }
            //Complete request
            app.CompleteRequest();
        }

        private async Task OnLeaveAsync(object source, EventArgs eventArgs) {
            var helper = new OutputCacheHelper(new HttpContextWrapper(((HttpApplication)source).Context));
            if (helper.IsResponseCacheable()) {
                await helper.CacheResponseAsync();
            }
        }
    }
}