using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.Test
{
    public class CachedVaryTest
    {
        private static readonly string[] DefaultEncodings = { "gzip", "deflate" };
        private static readonly string[] DefaultEncodingsWithUpperCase = { "Gzip", "Deflate" };
        private static readonly string[] DefaultHeaders = { "Accept-Charset", "Content-Encoding" };
        private static readonly string[] DefaultHeadersWithLowerCase = { "accept-charset", "content-encoding" };
        private static readonly string[] DefaultParams = { "param1", "param2" };
        private static readonly string[] DefaultParamsWithUpperCase = { "Param1", "Param2" };
        private static readonly Guid DefaultVaryId = Guid.NewGuid();
        private static readonly Guid AnotherVaryId = Guid.NewGuid();

        private const string DefaultVaryByCustom = "DefaultVaryByCustom";

        [Fact]
        public void Same_Object_Should_Equal()
        {
            var cv = new CachedVary();
            var cvSame = cv;

            Assert.True(cv.Equals(cvSame));
        }
        
        [Theory]
        [MemberData(nameof(VaryIdData))]
        public void Value_Of_CachedVaryId_Should_Not_Affect_Equal_Check(Guid cvId)
        {
            var cv = CreateCacheVaryWithDefaultValue();

            var anotherCv = CreateCacheVaryWithDefaultValue();
            anotherCv.CachedVaryId = cvId;

            Assert.True(cv.Equals(anotherCv));
        }

        [Fact]
        public void ContentEncodings_Is_Case_Sensitive_In_Equal_Check()
        {
            var cv = CreateCacheVaryWithDefaultValue();

            var anotherCv = CreateCacheVaryWithDefaultValue();
            anotherCv.ContentEncodings = DefaultEncodingsWithUpperCase;

            Assert.False(cv.Equals(anotherCv));
        }

        [Fact]
        public void Headers_Is_Case_Sensitive_In_Equal_Check()
        {
            var cv = CreateCacheVaryWithDefaultValue();

            var anotherCv = CreateCacheVaryWithDefaultValue();
            anotherCv.Headers = DefaultHeadersWithLowerCase;

            Assert.False(cv.Equals(anotherCv));
        }

        [Fact]
        public void Params_Is_Case_Sensitive_In_Equal_Check()
        {
            var cv = CreateCacheVaryWithDefaultValue();

            var anotherCv = CreateCacheVaryWithDefaultValue();
            anotherCv.Params = DefaultParamsWithUpperCase;

            Assert.False(cv.Equals(anotherCv));
        }

        public static IEnumerable<object[]> VaryIdData => new List<object[]>
        {
            new object[] { DefaultVaryId },
            new object[] { AnotherVaryId }
        };

        private static CachedVary CreateCacheVaryWithDefaultValue()
        {
            return new CachedVary()
            {
                CachedVaryId = DefaultVaryId,
                ContentEncodings = DefaultEncodings,
                Headers = DefaultHeaders,
                Params = DefaultParams,
                VaryByAllParams = true,
                VaryByCustom = DefaultVaryByCustom
            };
        }
    }
}
