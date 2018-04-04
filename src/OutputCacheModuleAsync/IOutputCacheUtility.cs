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
    internal interface IOutputCacheUtility
    {
        void SetContentBuffers(HttpContextBase context, ArrayList buffers);

        ArrayList GetContentBuffers(HttpContextBase context);

        string SetupKernelCaching(string originalCacheUrl, HttpContextBase context);

        CacheDependency CreateCacheDependency(HttpContextBase context);

        IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> GetValidationCallbacks(HttpContextBase context);

        string GetVaryByCustomString(HttpContextBase context, string custom);

        OutputCacheProviderAsync GetOutputCacheProvider(HttpContextBase context, string providerName);

        HttpContext GetContextFromHttpContextBase(HttpContextBase context);

        HttpCachePolicy GetCachePolicyFromHttpContextBase(HttpContextBase context);
    }
}
