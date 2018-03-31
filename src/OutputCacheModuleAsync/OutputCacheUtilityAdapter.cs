using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;

namespace Microsoft.AspNet.OutputCache
{
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

        public string GetOutputCacheProviderName(HttpContextBase context)
        {
            return context.ApplicationInstance.GetOutputCacheProviderName(context.ApplicationInstance.Context);
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
