// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Web;
    using System.Web.Caching;

    class OutputCacheUtilityAdapter : IOutputCacheUtility
    {
        public CacheDependency CreateCacheDependency(HttpContextBase context)
        {
            return OutputCacheUtility.CreateCacheDependency(context.ApplicationInstance.Response);
        }

        public HttpCachePolicy GetCachePolicyFromHttpContextBase(HttpContextBase context)
        {
            return context.ApplicationInstance.Response.Cache;
        }

        public ArrayList GetContentBuffers(HttpContextBase context)
        {
            return OutputCacheUtility.GetContentBuffers(context.ApplicationInstance.Response);
        }

        public HttpContext GetContextFromHttpContextBase(HttpContextBase context)
        {
            return context.ApplicationInstance.Context;
        }

        public OutputCacheProviderAsync GetOutputCacheProvider(HttpContextBase context, string providerName)
        {
            if (string.IsNullOrEmpty(providerName)) {
                providerName = context.ApplicationInstance.GetOutputCacheProviderName(context.ApplicationInstance.Context);
            }
            
            if (System.Web.Caching.OutputCache.Providers != null) {
                return System.Web.Caching.OutputCache.Providers[providerName] as OutputCacheProviderAsync;
            }

            return null;
        }

        public IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> GetValidationCallbacks(HttpContextBase context)
        {
            return OutputCacheUtility.GetValidationCallbacks(context.ApplicationInstance.Response);
        }

        public string GetVaryByCustomString(HttpContextBase context, string custom)
        {
            return context.ApplicationInstance.GetVaryByCustomString(
                    context.ApplicationInstance.Context, custom);
        }

        public void SetContentBuffers(HttpContextBase context, ArrayList buffers)
        {
            OutputCacheUtility.SetContentBuffers(context.ApplicationInstance.Response, buffers);
        }

        public string SetupKernelCaching(string originalCacheUrl, HttpContextBase context)
        {
            return OutputCacheUtility.SetupKernelCaching(originalCacheUrl, context.ApplicationInstance.Response);
        }
    }
}
