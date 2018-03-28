namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Web;
    using System.Web.Caching;

    class Converter {
        private const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type HttpFileResponseElementType;
        Type HttpSubstBlockResponseElementType;
        Type IHttpResponseElementType;
        Type HttpResponseBufferElementType;

        ConstructorInfo HttpResponseBufferElement_Ctor;
        ConstructorInfo HttpSubstBlockResponseElement_Ctor;
        ConstructorInfo HttpFileResponseElement_Ctor;

        MethodInfo IHttpResponseElement_GetBytes;
        MethodInfo IHttpResponseElement_GetSize;

        FieldInfo HttpFileResponseElement_FileName;
        FieldInfo HttpFileResponseElement_Offset;
        FieldInfo HttpFileResponseElement_IsImpersonating;
        FieldInfo HttpFileResponseElement_UseTransmitFile;
        FieldInfo HttpSubstBlockResponseElement_Callback;

        public Converter() {
            //
            // Initialize reflection data
            Assembly System_Web = typeof(ResponseElement).Assembly;

            //
            // Types
            HttpFileResponseElementType = System_Web.GetType("System.Web.HttpFileResponseElement");
            HttpSubstBlockResponseElementType = System_Web.GetType("System.Web.HttpSubstBlockResponseElement");
            IHttpResponseElementType = System_Web.GetType("System.Web.IHttpResponseElement");
            HttpResponseBufferElementType = System_Web.GetType("System.Web.HttpResponseBufferElement");

            //
            // Ctors
            HttpResponseBufferElement_Ctor = HttpResponseBufferElementType.GetConstructor(bindingFlags, null, new Type[] { typeof(Byte[]), typeof(Int32) }, null);
            HttpSubstBlockResponseElement_Ctor = HttpSubstBlockResponseElementType.GetConstructor(bindingFlags, null, new Type[] { typeof(HttpResponseSubstitutionCallback) }, null);
            HttpFileResponseElement_Ctor = HttpFileResponseElementType.GetConstructor(bindingFlags, null, new Type[] { typeof(string), typeof(long), typeof(long), typeof(bool), typeof(bool) }, null);

            // Methods
            IHttpResponseElement_GetBytes = IHttpResponseElementType.GetMethod("GetBytes");
            IHttpResponseElement_GetSize = IHttpResponseElementType.GetMethod("GetSize");

            // Fileds
            HttpFileResponseElement_FileName = HttpFileResponseElementType.GetField("_filename", bindingFlags);
            HttpFileResponseElement_Offset = HttpFileResponseElementType.GetField("_offset", bindingFlags);
            HttpFileResponseElement_IsImpersonating = HttpFileResponseElementType.GetField("_isImpersonating", bindingFlags);
            HttpFileResponseElement_UseTransmitFile = HttpFileResponseElementType.GetField("_useTransmitFile", bindingFlags);
            HttpSubstBlockResponseElement_Callback = HttpSubstBlockResponseElementType.GetField("_callback", bindingFlags);
        }


        public OutputCacheEntry CreateOutputCacheEntry(CachedRawResponse cachedRawResponse, string depKey, string[] fileDependencies) {
            //
            // Do the converting from FX internal IHttpResponseElement classes to public ResponseElement classes
            List<ResponseElement> responseElements = new List<ResponseElement>();
            foreach (var buffer in cachedRawResponse.RawResponse.Buffers) {
                Type type = buffer.GetType();
                ResponseElement elem = null;
                //
                // HttpFileResponseElement
                if (type == HttpFileResponseElementType) {
                    elem = CreateFileResponseElement(buffer);
                }
                //
                // HttpSubstBlockResponseElement
                else if (type == HttpSubstBlockResponseElementType) {
                    elem = CreateSubstBlockResponseElement(buffer);
                }
                //
                // IHttpResponseElement
                else {
                    elem = CreateMemoryResponseElement(buffer);
                }
                if (elem != null) {
                    responseElements.Add(elem);
                }
            }

            return new OutputCacheEntry() {
                CachedVaryId = cachedRawResponse.CachedVaryId,
                Settings = cachedRawResponse.CachePolicy,
                KernelCacheUrl = cachedRawResponse.KernelCacheUrl,
                DependenciesKey = depKey,
                Dependencies = fileDependencies,
                StatusCode = cachedRawResponse.RawResponse.StatusCode,
                StatusDescription = cachedRawResponse.RawResponse.StatusDescription,
                HeaderElements = cachedRawResponse.RawResponse.Headers,
                ResponseBuffers = responseElements
            };
        }

        public CachedRawResponse CreateCachedRawResponse(OutputCacheEntry oce) {
            ArrayList rawBuffers = new ArrayList();
            foreach (var re in oce.ResponseBuffers) {
                // convert the public ResponseElement classes back to IHttpResponseElement internal classes
                object elem = null;
                if (re is OutputCacheFileResponseElement) {
                    elem = CreateHttpFileResponseElement((OutputCacheFileResponseElement)re);
                }
                else if (re is SubstitutionResponseElement) {
                    elem = CreateHttpSubstBlockResponseElement((SubstitutionResponseElement)re);
                }
                else if (re is MemoryResponseElement) {
                    elem = CreateHttpResponseBufferElement((MemoryResponseElement)re);
                }
                if (elem != null) {
                    rawBuffers.Add(elem);
                }
            }
            return new CachedRawResponse {
                RawResponse = new HttpRawResponse {
                    StatusCode = oce.StatusCode,
                    StatusDescription = oce.StatusDescription,
                    Headers = oce.HeaderElements,
                    Buffers = rawBuffers
                },
                CachePolicy = oce.Settings,
                KernelCacheUrl = oce.KernelCacheUrl,
                CachedVaryId = oce.CachedVaryId
            };
        }

        private ResponseElement CreateFileResponseElement(object o) {
            return new OutputCacheFileResponseElement((string)HttpFileResponseElement_FileName.GetValue(o),
                (long)HttpFileResponseElement_Offset.GetValue(o),
                (long)IHttpResponseElement_GetSize.Invoke(o, new object[] { }),
                (bool)HttpFileResponseElement_IsImpersonating.GetValue(o),
                (bool)HttpFileResponseElement_UseTransmitFile.GetValue(o));
        }

        private ResponseElement CreateSubstBlockResponseElement(object o) {
            return new SubstitutionResponseElement((HttpResponseSubstitutionCallback)HttpSubstBlockResponseElement_Callback.GetValue(o));
        }

        private ResponseElement CreateMemoryResponseElement(object o) {
            byte[] b = (byte[])IHttpResponseElement_GetBytes.Invoke(o, new object[] { });
            long length = (b != null) ? b.Length : 0;
            return new MemoryResponseElement(b, length);
        }

        private object CreateHttpFileResponseElement(OutputCacheFileResponseElement fre) {
            //[Lan] how about we extend FileResponseElement class to store those two bool value
            //      HttpContext context = HttpContext.Current;
            //      HttpWorkerRequest wr = (context != null) ? context.WorkerRequest : null;
            //     bool supportsLongTransmitFile = (wr != null && wr.SupportsLongTransmitFile);
            //      bool isImpersonating = ((context != null && context.IsClientImpersonationConfigured) || HttpRuntime.IsOnUNCShareInternal);

            // DevDiv #21203: Need to verify permission to access the requested file since handled by native code.
            // [Lan] It only throw if no permission. so skip
            // HttpRuntime.CheckFilePermission(fre.Path);

            return HttpFileResponseElement_Ctor.Invoke(new object[] { fre.Path, fre.Offset, fre.Length, fre.IsImpersonating, fre.SupportsLongTransmitFile });
        }

        private object CreateHttpSubstBlockResponseElement(SubstitutionResponseElement sre) {            
            return HttpSubstBlockResponseElement_Ctor.Invoke(new object[] { sre.Callback });
        }

        private object CreateHttpResponseBufferElement(MemoryResponseElement e) {
            int size = System.Convert.ToInt32(e.Length);
            return HttpResponseBufferElement_Ctor.Invoke(new object[] { e.Buffer, size });
        }
    }
}
