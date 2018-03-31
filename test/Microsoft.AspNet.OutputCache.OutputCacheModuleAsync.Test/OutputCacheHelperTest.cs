﻿using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Web;
using Xunit;

namespace Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.Test {
    public class OutputCacheHelperTest {
        private const string AcceptEncodingHeaderName = "Accept-Encoding";
        private const string ContentEncodingHeaderName = "Content-Encoding";
        private const string CacheControlHeaderName = "Cache-Control";
        private const string RangeHeaderName = "Range";
        private const string PragmaHeaderName = "Pragma";
        private const string HttpMethods_POST = "POST";
        private const string HttpMethods_HEAD = "HEAD";
        private const string HttpMethods_GET = "GET";
        private const string HttpMethods_PUT = "PUT";
        private const string HttpMethods_DELETE = "DELETE";
        private const string HttpMethods_OPTIONS = "OPTIONS";
        private const string HttpMethods_CONNECT = "CONNECT";
        private static readonly string[] DefaultEncodings = { "gzip", "deflate" };

        [Fact]
        public void IsContentEncodingAcceptable_Should_Return_True_If_ContentEncodings_Is_Not_Null() {
            var httpContextMoq = new Mock<HttpContextBase>();
            var ocHelper = new OutputCacheHelper(httpContextMoq.Object);

            var cv = new CachedVary() { ContentEncodings = DefaultEncodings };
            var acceptable = ocHelper.IsContentEncodingAcceptable(cv, null);

            Assert.True(acceptable);

            cv = new CachedVary() { ContentEncodings = new string[0] };
            acceptable = ocHelper.IsContentEncodingAcceptable(cv, null);

            Assert.True(acceptable);
        }

        [Fact]
        public void IsContentEncodingAcceptable_Should_Return_True_If_Not_Have_ContentEncoding_And_AcceptableEncoding_Headers() {
            var request = new Mock<HttpRequestBase>();
            request.Setup(r => r.Headers).Returns(new NameValueCollection());
            var httpContextMoq = new Mock<HttpContextBase>();
            httpContextMoq.Setup(ctx => ctx.Request).Returns(request.Object);
            var ocHelper = new OutputCacheHelper(httpContextMoq.Object);

            var acceptable = ocHelper.IsContentEncodingAcceptable(null, new HttpRawResponse());

            Assert.True(acceptable);
        }

        [Fact]
        public void IsContentEncodingAcceptable_Should_Return_True_If_ContentEncoding_And_AcceptableEncoding_Headers_Match() {
            var request = new Mock<HttpRequestBase>();
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(AcceptEncodingHeaderName, "gzip");
            request.Setup(r => r.Headers).Returns(requestHeaders);
            var httpContextMoq = new Mock<HttpContextBase>();
            httpContextMoq.Setup(ctx => ctx.Request).Returns(request.Object);
            var rawResponse = new HttpRawResponse();
            rawResponse.Headers = new NameValueCollection();
            rawResponse.Headers.Add(ContentEncodingHeaderName, "gzip");
            var ocHelper = new OutputCacheHelper(httpContextMoq.Object);


            var acceptable = ocHelper.IsContentEncodingAcceptable(null, rawResponse);

            Assert.True(acceptable);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "identity")]
        [InlineData("identity", "")]
        [InlineData("gzip", "deflate, gzip")]
        [InlineData("gzip", "deflate, gzip;q=1.0")]
        [InlineData("gzip", "deflate, gzip;q=-1.0, *;q=0.5")]
        public void IsAcceptableEncoding_Should_Return_True_If_ContentEncoding_Matches_AcceptEncoding(string contentEncoding,
            string acceptEncoding) {
            var httpContextMoq = new Mock<HttpContextBase>();
            var ocHelper = new OutputCacheHelper(httpContextMoq.Object);

            var acceptable = ocHelper.IsAcceptableEncoding(contentEncoding, acceptEncoding);

            Assert.True(acceptable);
        }

        [Theory]
        [InlineData("gzip", "")]
        [InlineData("gzip", "deflate, gzip;q=0")]
        [InlineData("br", "deflate, gzip, *;q=0")]
        public void IsAcceptableEncoding_Should_Return_False_If_ContentEncoding_Not_Matches_AcceptEncoding(string contentEncoding,
            string acceptEncoding) {
            var httpContextMoq = new Mock<HttpContextBase>();
            var ocHelper = new OutputCacheHelper(httpContextMoq.Object);

            var acceptable = ocHelper.IsAcceptableEncoding(contentEncoding, acceptEncoding);

            Assert.False(acceptable);
        }

        [Fact]
        public void CheckHeaders_Should_Return_False_If_HttpCachePolicySettings_HasValidationPolicy() {
            var httpContextMoq = new Mock<HttpContextBase>();
            var ocHelper = new OutputCacheHelper(httpContextMoq.Object);
            var settings = new HttpCachePolicySettings() { GenerateEtagFromFiles = true };

            var result = ocHelper.CheckHeaders(settings);

            Assert.False(result);
        }

        [Fact]
        public void CheckHeaders_Should_Return_False_If_RequestHeaders_Not_Have_CacheControl_And_Pragma_Header() {
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase());
            var settings = new HttpCachePolicySettings() { GenerateEtagFromFiles = true };

            var result = ocHelper.CheckHeaders(settings);

            Assert.False(result);
        }

        [Fact]
        public void CheckHeaders_Should_Return_True_If_CacheControl_Is_NoCache() {
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(CacheControlHeaderName, "no-cache");
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase(requestHeaders));
            var settings = new HttpCachePolicySettings() {
                SlidingExpiration = true,
                GenerateLastModifiedFromFiles = false,
                GenerateEtagFromFiles = false,
                ValidationCallbackInfo = null
            };

            var result = ocHelper.CheckHeaders(settings);

            Assert.True(result);
        }

        [Fact]
        public void CheckHeaders_Should_Return_False_If_Pragma_Is_Empty_Or_NoneNoCache() {
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase());
            var settings = new HttpCachePolicySettings() {
                SlidingExpiration = true,
                GenerateLastModifiedFromFiles = false,
                GenerateEtagFromFiles = false,
                ValidationCallbackInfo = null
            };

            var result = ocHelper.CheckHeaders(settings);

            Assert.False(result);

            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(PragmaHeaderName, "some-token");
            ocHelper = new OutputCacheHelper(CreateHttpContextBase(requestHeaders));

            result = ocHelper.CheckHeaders(settings);

            Assert.False(result);
        }

        [Fact]
        public void CheckHeaders_Should_Return_True_If_Pragma_Is_NoCache() {
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(PragmaHeaderName, "no-cache");
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase(requestHeaders));
            var settings = new HttpCachePolicySettings() {
                SlidingExpiration = true,
                GenerateLastModifiedFromFiles = false,
                GenerateEtagFromFiles = false,
                ValidationCallbackInfo = null
            };

            var result = ocHelper.CheckHeaders(settings);

            Assert.True(result);
        }

        [Fact]
        public async void CheckValidityAsync_Should_Do_Nothing_And_Return_False_If_ValidationCallbackInfo_Is_Empty() {
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase());
            var settings = new HttpCachePolicySettings();

            var result = await ocHelper.CheckValidityAsync("key", settings);

            Assert.False(result);

            settings.ValidationCallbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();

            result = await ocHelper.CheckValidityAsync("key1", settings);

            Assert.False(result);
        }

        [Fact]
        public async void CheckValidityAsync_Should_Return_False_If_ValidationStatus_Is_Not_IgnoreThisRequest() {
            var cacheUtilMoq = new Mock<IOutputCacheUtility>();
            var context = CreateHttpContextBase();
            cacheUtilMoq.Setup(util => util.GetContextFromHttpContextBase(context));
            var ocHelper = new OutputCacheHelper(context, cacheUtilMoq.Object);
            var settings = new HttpCachePolicySettings();
            var callbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
            var kv = new KeyValuePair<HttpCacheValidateHandler, object>(HttpCacheValidateHandlerReturnValidStatus, "test");
            callbackInfo.Add(kv);
            settings.ValidationCallbackInfo = callbackInfo;

            var result = await ocHelper.CheckValidityAsync("key", settings);

            Assert.False(result);
        }

        [Fact]
        public async void CheckValidityAsync_Should_Return_True_If_ValidationStatus_Is_Not_IgnoreThisRequest() {
            var cacheUtilMoq = new Mock<IOutputCacheUtility>();
            var context = CreateHttpContextBase();
            cacheUtilMoq.Setup(util => util.GetContextFromHttpContextBase(context));
            var ocHelper = new OutputCacheHelper(context, cacheUtilMoq.Object);
            var settings = new HttpCachePolicySettings();
            var callbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
            var kv = new KeyValuePair<HttpCacheValidateHandler, object>(HttpCacheValidateHandlerReturnIgnoreThisRequestStatus, "test");
            callbackInfo.Add(kv);
            settings.ValidationCallbackInfo = callbackInfo;

            var result = await ocHelper.CheckValidityAsync("key", settings);

            Assert.True(result);
        }

        [Theory]
        [InlineData(HttpMethods_POST)]
        [InlineData(HttpMethods_HEAD)]
        [InlineData(HttpMethods_GET)]
        public void IsHttpMethodSupported_Return_True_Only_If_HttpMethod_Is_Head_Get_Post(string httpMethod) {
            var requestMoq = new Mock<HttpRequestBase>();
            requestMoq.Setup(r => r.HttpMethod).Returns(httpMethod);
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase(requestMoq.Object));

            var result = ocHelper.IsHttpMethodSupported();

            Assert.True(result);
        }

        [Theory]
        [InlineData(HttpMethods_PUT)]
        [InlineData(HttpMethods_DELETE)]
        [InlineData(HttpMethods_OPTIONS)]
        [InlineData(HttpMethods_CONNECT)]
        public void IsHttpMethodSupported_Return_False_Only_If_HttpMethod_Is_Not_Head_Get_Post(string httpMethod) {
            var requestMoq = new Mock<HttpRequestBase>();
            requestMoq.Setup(r => r.HttpMethod).Returns(httpMethod);
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase(requestMoq.Object));

            var result = ocHelper.IsHttpMethodSupported();

            Assert.False(result);
        }

        [Fact]
        public void CreateOutputCachedItemKey_Should_Create_Correct_CacheKey_For_Post_Requst() {
            var requestMoq = new Mock<HttpRequestBase>();
            requestMoq.Setup(r => r.Path).Returns("test.aspx");
            requestMoq.Setup(r => r.HttpMethod).Returns(HttpMethods_POST);
            var srvVars = new NameValueCollection();
            srvVars.Add("AUTH_TYPE", "Basic");
            srvVars.Add("HTTP_HOST", "localhost");
            requestMoq.Setup(r => r.ServerVariables).Returns(srvVars);
            var queryStrs = new NameValueCollection();
            queryStrs.Add("query1", "1");
            queryStrs.Add("Query2", "aB");            
            requestMoq.Setup(r => r.QueryString).Returns(queryStrs);
            var forms = new NameValueCollection();
            forms.Add("form1", "1");
            forms.Add("Form2", "Cd");
            requestMoq.Setup(r => r.Form).Returns(forms);
            var headers = new NameValueCollection();
            headers.Add(AcceptEncodingHeaderName, "gzip,deflate");
            requestMoq.Setup(r => r.Headers).Returns(headers);

            var context = CreateHttpContextBase(requestMoq.Object);
            var cacheUtilMoq = new Mock<IOutputCacheUtility>();
            cacheUtilMoq.Setup(util => util.GetVaryByCustomString(context, "CustomVary")).Returns("UtilCustomVary");
            var ocHelper = new OutputCacheHelper(context, cacheUtilMoq.Object);

            var key = ocHelper.CreateOutputCachedItemKey(null);
            Assert.Equal("a1test.aspx", key);

            var cv = new CachedVary() { VaryByCustom = "CustomVary" };
            key = ocHelper.CreateOutputCachedItemKey(cv);
            Assert.Equal("a1test.aspxHQFCNCustomVaryVUtilCustomVaryDE", key);

            cv = new CachedVary() {
                Headers = new string[] { "AUTH_TYPE", "HTTP_HOST" },
                Params = new string[] { "form1", "Form2" },
                VaryByAllParams = true
            };
            key = ocHelper.CreateOutputCachedItemKey(cv);
            Assert.Equal("a1test.aspxHNAUTH_TYPEVBasicNHTTP_HOSTVlocalhostQNquery1V1Nquery2VaBFNform1V1Nform2VCdCDE", key);

            cv = new CachedVary() {
                Params = new string[] { "form1", "Form2" },
                VaryByAllParams = true,
                ContentEncodings = new string[] { "gzip", "deflate" }
            };
            key = ocHelper.CreateOutputCachedItemKey(cv);
            Assert.Equal("a1test.aspxHQNquery1V1Nquery2VaBFNform1V1Nform2VCdCDEgzip", key);
        }

        [Fact]
        public void CreateOutputCachedItemKey_Should_Create_Correct_CacheKey_For_NonePost_Requst() {
            var requestMoq = new Mock<HttpRequestBase>();
            requestMoq.Setup(r => r.Path).Returns("test.aspx");
            requestMoq.Setup(r => r.HttpMethod).Returns(HttpMethods_GET);
            var srvVars = new NameValueCollection();
            srvVars.Add("AUTH_TYPE", "Basic");
            srvVars.Add("HTTP_HOST", "localhost");
            requestMoq.Setup(r => r.ServerVariables).Returns(srvVars);
            var queryStrs = new NameValueCollection();
            queryStrs.Add("query1", "1");
            queryStrs.Add("Query2", "aB");
            requestMoq.Setup(r => r.QueryString).Returns(queryStrs);
            var forms = new NameValueCollection();
            forms.Add("form1", "1");
            forms.Add("Form2", "Cd");
            requestMoq.Setup(r => r.Form).Returns(forms);
            var headers = new NameValueCollection();
            headers.Add(AcceptEncodingHeaderName, "gzip,deflate");
            requestMoq.Setup(r => r.Headers).Returns(headers);

            var context = CreateHttpContextBase(requestMoq.Object);
            var cacheUtilMoq = new Mock<IOutputCacheUtility>();
            cacheUtilMoq.Setup(util => util.GetVaryByCustomString(context, "CustomVary")).Returns("UtilCustomVary");
            var ocHelper = new OutputCacheHelper(context, cacheUtilMoq.Object);

            var key = ocHelper.CreateOutputCachedItemKey(null);
            Assert.Equal("a2test.aspx", key);

            var cv = new CachedVary() { VaryByCustom = "CustomVary" };
            key = ocHelper.CreateOutputCachedItemKey(cv);
            Assert.Equal("a2test.aspxHQFCNCustomVaryVUtilCustomVaryDE", key);

            cv = new CachedVary() {
                Headers = new string[] { "AUTH_TYPE", "HTTP_HOST" },
                Params = new string[] { "form1", "Form2" },
                VaryByAllParams = true
            };
            key = ocHelper.CreateOutputCachedItemKey(cv);
            Assert.Equal("a2test.aspxHNAUTH_TYPEVBasicNHTTP_HOSTVlocalhostQNquery1V1Nquery2VaBFCDE", key);

            cv = new CachedVary() {
                Params = new string[] { "form1", "Form2" },
                VaryByAllParams = true,
                ContentEncodings = new string[] { "gzip", "deflate" }
            };
            key = ocHelper.CreateOutputCachedItemKey(cv);
            Assert.Equal("a2test.aspxHQNquery1V1Nquery2VaBFCDEgzip", key);
        }

        [Fact]
        public void IsRangeRequest_Should_Return_True_Only_If_BytesRange_In_RequestHeader() {
            var headers = new NameValueCollection();
            headers.Add(RangeHeaderName, "bytes=200-1000, 2000-6576");
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase(headers));

            var result = ocHelper.IsRangeRequest();
            Assert.True(result);

            ocHelper = new OutputCacheHelper(CreateHttpContextBase());
            result = ocHelper.IsRangeRequest();
            Assert.False(result);

            headers = new NameValueCollection();
            headers.Add(RangeHeaderName, "otherunit=200-1000, 2000-6576");
            ocHelper = new OutputCacheHelper(CreateHttpContextBase());
            result = ocHelper.IsRangeRequest();
            Assert.False(result);
        }

        [Theory]
        [MemberData(nameof(GetAcceptableEncodingTestData))]
        public void GetAcceptableEncoding_Should_ReturnAcceptable_Index_In_ContentEncodings(string[] contentEncodings,
            int startIndex, string acceptEncoding, int expectedIndex) {
            var ocHelper = new OutputCacheHelper(CreateHttpContextBase());

            var result = ocHelper.GetAcceptableEncoding(contentEncodings, startIndex, acceptEncoding);

            Assert.Equal(expectedIndex, result);
        }

        public static IEnumerable<object[]> GetAcceptableEncodingTestData => new List<object[]>
        {
            new object[] { null, 0, "",  -1},
            new object[] {null, 0, "*", 0},
            new object[] {null, 0, "*; q=0", -2},
            new object[] {null, 0, "identity; q=0", -2},
            new object[] {new string[] {"gzip", "deflate" }, 0, "gzip", 0},
            new object[] {new string[] {"gzip", "deflate" }, 0, "gzip; q=0", -2},
            new object[] {new string[] {"gzip", "deflate" }, 0, "gzip; q=0.5", 0},
            new object[] {new string[] {"gzip", "deflate" }, 1, "gzip", -2}
        };

        private HttpContextBase CreateHttpContextBase(NameValueCollection requestHeaders = null) {
            var requestMoq = new Mock<HttpRequestBase>();
            requestMoq.Setup(r => r.Headers).Returns(requestHeaders ?? new NameValueCollection());
            var httpContextMoq = new Mock<HttpContextBase>();
            httpContextMoq.Setup(ctx => ctx.Request).Returns(requestMoq.Object);

            return httpContextMoq.Object;
        }

        private HttpContextBase CreateHttpContextBase(HttpRequestBase request) {
            var httpContextMoq = new Mock<HttpContextBase>();
            httpContextMoq.Setup(ctx => ctx.Request).Returns(request);

            return httpContextMoq.Object;
        }

        private static void HttpCacheValidateHandlerReturnIgnoreThisRequestStatus(HttpContext context, object data, ref HttpValidationStatus validationStatus) {
            validationStatus = HttpValidationStatus.IgnoreThisRequest;
        }

        private static void HttpCacheValidateHandlerReturnValidStatus(HttpContext context, object data, ref HttpValidationStatus validationStatus) {
            validationStatus = HttpValidationStatus.Valid;
        }
    }
}
