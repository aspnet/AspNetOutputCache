// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.Test
{
    using System.Collections.Generic;
    using System.Web;
    using Xunit;

    public class HttpCachePolicySettingsTest
    {
        private static readonly IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> DefaultCallbackInfo;

        static HttpCachePolicySettingsTest()
        {
            DefaultCallbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
        }


        [Fact]
        public void ValidUntilExpires_Should_Be_True()
        {
            var setting = new HttpCachePolicySettings()
            {
                SlidingExpiration = false,
                GenerateLastModifiedFromFiles = false,
                GenerateEtagFromFiles = false,
                ValidationCallbackInfo = null
            };

            Assert.True(setting.ValidUntilExpires);
        }

        [Theory]
        [MemberData(nameof(ValidUntilExpiresTestData))]
        public void ValidUntilExpires_Should_Be_False(bool slidingExp, bool genLastModifiedFromFiles,
            bool genEtagFromFiles, IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> callbackInfo)
        {
            var setting = new HttpCachePolicySettings()
            {
                SlidingExpiration = slidingExp,
                GenerateLastModifiedFromFiles = genLastModifiedFromFiles,
                GenerateEtagFromFiles = genEtagFromFiles,
                ValidationCallbackInfo = callbackInfo
            };

            Assert.False(setting.ValidUntilExpires);
        }

        [Fact]
        public void IsValidationCallbackSerializable_Should_Be_True_When_All_HttpCacheValidateHandler_Are_Static()
        {
            var callbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
            callbackInfo.Add(new KeyValuePair<HttpCacheValidateHandler, object>(StaticHttpCacheValidateHandler1, 1));
            callbackInfo.Add(new KeyValuePair<HttpCacheValidateHandler, object>(StaticHttpCacheValidateHandler2, 2));

            var setting = new HttpCachePolicySettings()
            {
                ValidationCallbackInfo = callbackInfo
            };
            Assert.True(setting.IsValidationCallbackSerializable());

            callbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
            setting = new HttpCachePolicySettings()
            {
                ValidationCallbackInfo = callbackInfo
            };
            Assert.True(setting.IsValidationCallbackSerializable());

            callbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
            callbackInfo.Add(new KeyValuePair<HttpCacheValidateHandler, object>(HttpCacheValidateHandler1, 1));
            setting = new HttpCachePolicySettings()
            {
                ValidationCallbackInfo = callbackInfo
            };
            Assert.False(setting.IsValidationCallbackSerializable());

            callbackInfo = new List<KeyValuePair<HttpCacheValidateHandler, object>>();
            callbackInfo.Add(new KeyValuePair<HttpCacheValidateHandler, object>(HttpCacheValidateHandler1, 1));
            callbackInfo.Add(new KeyValuePair<HttpCacheValidateHandler, object>(StaticHttpCacheValidateHandler1, 1));
            setting = new HttpCachePolicySettings()
            {
                ValidationCallbackInfo = callbackInfo
            };
            Assert.False(setting.IsValidationCallbackSerializable());
        }

        [Theory]
        [MemberData(nameof(HasValidationPolicyTrueConditionTestData))]
        public void HasValidationPolicy_Should_Be_True(bool slidingExp, bool genLastModifiedFromFiles,
            bool genEtagFromFiles, IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> callbackInfo)
        {
            var setting = new HttpCachePolicySettings()
            {
                SlidingExpiration = slidingExp,
                GenerateLastModifiedFromFiles = genLastModifiedFromFiles,
                GenerateEtagFromFiles = genEtagFromFiles,
                ValidationCallbackInfo = callbackInfo
            };

            Assert.True(setting.HasValidationPolicy());
        }

        [Theory]
        [MemberData(nameof(HasValidationPolicyFalseConditionTestData))]
        public void HasValidationPolicy_Should_Be_False(bool slidingExp, bool genLastModifiedFromFiles,
            bool genEtagFromFiles, IEnumerable<KeyValuePair<HttpCacheValidateHandler, object>> callbackInfo)
        {
            var setting = new HttpCachePolicySettings()
            {
                SlidingExpiration = slidingExp,
                GenerateLastModifiedFromFiles = genLastModifiedFromFiles,
                GenerateEtagFromFiles = genEtagFromFiles,
                ValidationCallbackInfo = callbackInfo
            };

            Assert.False(setting.HasValidationPolicy());
        }

        public static IEnumerable<object[]> ValidUntilExpiresTestData => new List<object[]>
        {
            new object[] { true, false, false,  DefaultCallbackInfo},
            new object[] { false, true, false,  DefaultCallbackInfo},
            new object[] { false, false, true,  DefaultCallbackInfo},
            new object[] { true, true, false,  DefaultCallbackInfo},
            new object[] { true, true, false,  null},
            new object[] { false, true, true,  null},
            new object[] { true, true, true,  DefaultCallbackInfo},
        };

        public static IEnumerable<object[]> HasValidationPolicyTrueConditionTestData => new List<object[]>
        {
            new object[] { false, false, false,  DefaultCallbackInfo},
            new object[] { false, true, false,  null},
            new object[] { false, false, true,  null},
            new object[] { false, false, false,  DefaultCallbackInfo}
        };

        public static IEnumerable<object[]> HasValidationPolicyFalseConditionTestData => new List<object[]>
        {
            new object[] { true, false, false,  null}
        };

        private static void StaticHttpCacheValidateHandler1(HttpContext context, object data, ref HttpValidationStatus validationStatus)
        {
        }

        private static void StaticHttpCacheValidateHandler2(HttpContext context, object data, ref HttpValidationStatus validationStatus)
        {
        }

        private void HttpCacheValidateHandler1(HttpContext context, object data, ref HttpValidationStatus validationStatus)
        {
        }
    }
}
