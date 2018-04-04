using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using Xunit;

namespace Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.Test
{
    public class ConverterTest
    {
        private const BindingFlags InternalCtorBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string DefaultKernelCacheUrl = "localhost/cachetest";
        private static readonly Assembly SystemWebAssembly = typeof(System.Web.HttpContext).Assembly;
        private static readonly Guid DefaultVaryId = Guid.NewGuid();
        private static readonly NameValueCollection DefaultRawResponseHeaders;
        private const int DefaultRawResponseStatusCode = 200;
        private const string DefaultRawResponseStatusDescription = "OK";
        private const string DefaultDepKey = "Test";
        private static readonly string[] DefaultFileDep = {"test.aspx"};
        private static readonly HttpResponseSubstitutionCallback DefaultHttpResponseSubstitutionCallback;
        private static readonly Type HttpFileResponseElementType;
        private static readonly Type HttpSubstBlockResponseElementType;
        private static readonly Type HttpResponseBufferElementType;
        private static readonly Type IHttpResponseElementType;
        private static readonly MethodInfo IHttpResponseElement_GetBytes;
        private static readonly MethodInfo IHttpResponseElement_GetSize;
        private static readonly FieldInfo HttpFileResponseElement_FileName;
        private static readonly FieldInfo HttpFileResponseElement_Offset;
        private static readonly FieldInfo HttpFileResponseElement_IsImpersonating;
        private static readonly FieldInfo HttpFileResponseElement_UseTransmitFile;
        private static readonly FieldInfo HttpSubstBlockResponseElement_Callback;
        private static readonly byte[] DefaultHttpResponseBufferElementBuffer = new byte[10];
        private const long DefaultHttpResponseBufferElementBufferSize = 10;
        private const string DefaultHttpFileResponseElementFilePath = "test.aspx";
        private const long DefaultHttpFileResponseElementOffset = 0;
        private const long DefaultHttpFileResponseElementSize = 1000;

        static ConverterTest()
        {
            DefaultRawResponseHeaders = new NameValueCollection();
            DefaultRawResponseHeaders["Content-Encoding"] = "gzip";
            DefaultRawResponseHeaders["Content-Type"] = "text/html";
            DefaultRawResponseHeaders["Server"] = "Microsoft-IIS/8.0";

            DefaultHttpResponseSubstitutionCallback = new HttpResponseSubstitutionCallback(ctx => "test");

            HttpFileResponseElementType = SystemWebAssembly.GetType("System.Web.HttpFileResponseElement");
            HttpSubstBlockResponseElementType = SystemWebAssembly.GetType("System.Web.HttpSubstBlockResponseElement");
            HttpResponseBufferElementType = SystemWebAssembly.GetType("System.Web.HttpResponseBufferElement");
            IHttpResponseElementType = SystemWebAssembly.GetType("System.Web.IHttpResponseElement");

            // Methods
            IHttpResponseElement_GetBytes = IHttpResponseElementType.GetMethod("GetBytes");
            IHttpResponseElement_GetSize = IHttpResponseElementType.GetMethod("GetSize");

            // Fileds
            HttpFileResponseElement_FileName = HttpFileResponseElementType.GetField("_filename", InternalCtorBindingFlags);
            HttpFileResponseElement_Offset = HttpFileResponseElementType.GetField("_offset", InternalCtorBindingFlags);
            HttpFileResponseElement_IsImpersonating = HttpFileResponseElementType.GetField("_isImpersonating", InternalCtorBindingFlags);
            HttpFileResponseElement_UseTransmitFile = HttpFileResponseElementType.GetField("_useTransmitFile", InternalCtorBindingFlags);
            HttpSubstBlockResponseElement_Callback = HttpSubstBlockResponseElementType.GetField("_callback", InternalCtorBindingFlags);
        }

        [Fact]
        public void CreateOutputCacheEntry_Should_Create_Equal_OutputCacheEntry()
        {
            var buffers = new ArrayList();
            buffers.Add(CreateHttpFileResponseElementTypeInstance());
            buffers.Add(CreateHttpSubstBlockResponseElementTypeInstance());
            buffers.Add(CreateHttpResponseBufferElementTypeInstance());

            var rawResponse = new HttpRawResponse()
            {
                Headers = DefaultRawResponseHeaders,
                StatusCode = DefaultRawResponseStatusCode,
                StatusDescription = DefaultRawResponseStatusDescription,
                Buffers = buffers
            };

            var rawCacheResponse = new CachedRawResponse()
            {
                CachedVaryId = DefaultVaryId,
                CachePolicy = new HttpCachePolicySettings(),
                KernelCacheUrl = DefaultKernelCacheUrl,
                RawResponse = rawResponse
            };

            var converter = new Converter();
            var cacheEntry = converter.CreateOutputCacheEntry(rawCacheResponse, DefaultDepKey, DefaultFileDep);

            Assert.Equal(rawCacheResponse.CachedVaryId, cacheEntry.CachedVaryId);
            Assert.Equal(rawCacheResponse.CachePolicy, cacheEntry.Settings);
            Assert.Equal(rawCacheResponse.KernelCacheUrl, cacheEntry.KernelCacheUrl);
            Assert.Equal(DefaultDepKey, cacheEntry.DependenciesKey);
            Assert.Equal(DefaultFileDep, cacheEntry.Dependencies);
            Assert.Equal(rawCacheResponse.RawResponse.StatusCode, cacheEntry.StatusCode);
            Assert.Equal(rawCacheResponse.RawResponse.StatusDescription, cacheEntry.StatusDescription);
            Assert.Equal(rawCacheResponse.RawResponse.Headers, cacheEntry.HeaderElements);
            Assert.Collection(cacheEntry.ResponseBuffers, VerifyOutputCacheFileResponseElement, 
                    VerifySubstitutionResponseElement, VerifyMemoryResponseElement);
        }

        [Fact]
        public void CreateCachedRawResponse_Should_Create_Equal_CachedRawResponse()
        {
            var buffers = new List<ResponseElement>();
            buffers.Add(new OutputCacheFileResponseElement(DefaultHttpFileResponseElementFilePath,
                DefaultHttpFileResponseElementOffset, DefaultHttpFileResponseElementSize));
            buffers.Add(new SubstitutionResponseElement(DefaultHttpResponseSubstitutionCallback));
            buffers.Add(new MemoryResponseElement(DefaultHttpResponseBufferElementBuffer,
                DefaultHttpResponseBufferElementBufferSize));

            var oce = new OutputCacheEntry()
            {
                CachedVaryId = DefaultVaryId,
                Dependencies = DefaultFileDep,
                DependenciesKey = DefaultDepKey,
                HeaderElements = DefaultRawResponseHeaders,
                KernelCacheUrl = DefaultKernelCacheUrl,
                Settings = new HttpCachePolicySettings(),
                StatusCode = DefaultRawResponseStatusCode,
                StatusDescription = DefaultRawResponseStatusDescription,
                ResponseBuffers = buffers
            };
            var converter = new Converter();

            var cachedRawResponse = converter.CreateCachedRawResponse(oce);

            Assert.Equal(oce.CachedVaryId, cachedRawResponse.CachedVaryId);
            Assert.Equal(oce.Settings, cachedRawResponse.CachePolicy);
            Assert.Equal(oce.KernelCacheUrl, cachedRawResponse.KernelCacheUrl);
            Assert.Equal(oce.StatusCode, cachedRawResponse.RawResponse.StatusCode);
            Assert.Equal(oce.StatusDescription, cachedRawResponse.RawResponse.StatusDescription);
            Assert.Equal(oce.HeaderElements, cachedRawResponse.RawResponse.Headers);
            Assert.Equal(3, cachedRawResponse.RawResponse.Buffers.Count);
            VerifyHttpFileResponseElement(cachedRawResponse.RawResponse.Buffers[0]);
            VerifyHttpSubstBlockResponseElement(cachedRawResponse.RawResponse.Buffers[1]);
            VerifyHttpResponseBufferElement(cachedRawResponse.RawResponse.Buffers[2]);
        }

        private static object CreateHttpFileResponseElementTypeInstance()
        {
            var type = SystemWebAssembly.GetType("System.Web.HttpFileResponseElement");
            var ctor = type.GetConstructor(InternalCtorBindingFlags, null, 
                new Type[] { typeof(string), typeof(long), typeof(long), typeof(bool), typeof(bool) }, null);

            return ctor.Invoke(new object[]{ DefaultHttpFileResponseElementFilePath, 
                DefaultHttpFileResponseElementOffset, DefaultHttpFileResponseElementSize, false, false });
        }

        private static void VerifyOutputCacheFileResponseElement(ResponseElement element)
        {
            var cacheFileElement = (OutputCacheFileResponseElement)element;
            Assert.NotNull(cacheFileElement);
            Assert.Equal(DefaultHttpFileResponseElementFilePath, cacheFileElement.Path);
            Assert.Equal(DefaultHttpFileResponseElementOffset, cacheFileElement.Offset);
            Assert.Equal(DefaultHttpFileResponseElementSize, cacheFileElement.Length);
            Assert.False(cacheFileElement.IsImpersonating);
            Assert.True(cacheFileElement.SupportsLongTransmitFile);
        }

        private static void VerifyHttpFileResponseElement(object element)
        {
            Assert.Equal(HttpFileResponseElementType, element.GetType());
            Assert.Equal(DefaultHttpFileResponseElementFilePath, HttpFileResponseElement_FileName.GetValue(element));
            Assert.Equal(DefaultHttpFileResponseElementOffset, HttpFileResponseElement_Offset.GetValue(element));
            Assert.Equal(DefaultHttpFileResponseElementSize, IHttpResponseElement_GetSize.Invoke(element, new object[] { }));
            Assert.False((bool)HttpFileResponseElement_IsImpersonating.GetValue(element));
            Assert.True((bool)HttpFileResponseElement_UseTransmitFile.GetValue(element));
        }

        private static object CreateHttpSubstBlockResponseElementTypeInstance()
        {
            var type = SystemWebAssembly.GetType("System.Web.HttpSubstBlockResponseElement");
            var ctor = type.GetConstructor(InternalCtorBindingFlags, null, new Type[] { typeof(HttpResponseSubstitutionCallback) }, null);
            var callback = DefaultHttpResponseSubstitutionCallback;

            return ctor.Invoke(new object[] { callback });
        }

        private static void VerifySubstitutionResponseElement(ResponseElement element)
        {
            var httpBlockElement = (SubstitutionResponseElement)element;
            Assert.NotNull(httpBlockElement);
            Assert.Equal(DefaultHttpResponseSubstitutionCallback, httpBlockElement.Callback);
        }

        private static void VerifyHttpSubstBlockResponseElement(object element)
        {
            Assert.Equal(HttpSubstBlockResponseElementType, element.GetType());
            Assert.Equal(DefaultHttpResponseSubstitutionCallback, HttpSubstBlockResponseElement_Callback.GetValue(element));
        }

        private static object CreateHttpResponseBufferElementTypeInstance()
        {
            var type = SystemWebAssembly.GetType("System.Web.HttpResponseBufferElement");
            var ctor = type.GetConstructor(InternalCtorBindingFlags, null, new Type[] { typeof(byte[]), typeof(int) }, null);

            return ctor.Invoke(new object[] { DefaultHttpResponseBufferElementBuffer, (int)DefaultHttpResponseBufferElementBufferSize });
        }

        private static void VerifyMemoryResponseElement(ResponseElement element)
        {
            var memoryElement = (MemoryResponseElement)element;
            Assert.NotNull(memoryElement);
            Assert.Equal(DefaultHttpResponseBufferElementBufferSize, memoryElement.Length);
            Assert.Equal(DefaultHttpResponseBufferElementBuffer, memoryElement.Buffer);
        }

        private static void VerifyHttpResponseBufferElement(object element)
        {
            Assert.Equal(HttpResponseBufferElementType, element.GetType());
            Assert.Equal(DefaultHttpResponseBufferElementBuffer, IHttpResponseElement_GetBytes.Invoke(element, new object[] { }));
            Assert.Equal(DefaultHttpResponseBufferElementBufferSize, IHttpResponseElement_GetSize.Invoke(element, new object[] { }));
        }
    }
}
