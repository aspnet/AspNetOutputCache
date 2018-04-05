// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Web;
    using System.Web.Caching;

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
