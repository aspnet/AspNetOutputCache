using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider.Test
{
    public class BinarySerializerTest
    {
        [Fact]
        public void BinarySerializer_Can_RoundTrip_Primitive_Type() {
            var strObj = "test";
            var actualStr = RoundTrip(strObj);
            Assert.Equal(actualStr, actualStr);

            var intObj = 123;
            var actualInt = RoundTrip(intObj);
            Assert.Equal(intObj, actualInt);

            var doubleObj = 123.123d;
            var actualDouble = RoundTrip(doubleObj);
            Assert.Equal(doubleObj, actualDouble);

            var byteObj = 0x99;
            var actualByte = RoundTrip(byteObj);
            Assert.Equal(byteObj, actualByte);

            var boolObj = true;
            var actualBool = RoundTrip(boolObj);
            Assert.True(actualBool);
        }

        [Fact]
        public void BinarySerializer_Can_RoundTrip_CachedVary_Type() {
            CachedVary obj = null;
            var data = BinarySerializer.Serialize(obj);
            var actual = BinarySerializer.Deserialize(data);

            Assert.NotNull(actual);
            Assert.Equal(typeof(object), actual.GetType());

            obj = new CachedVary() {
                CachedVaryId = Guid.NewGuid(),
                ContentEncodings = new string[] { "111", "222" },
                Headers = new string[] { "Get", "Put" },
                Params = new string[] { "test", "test1" },
                VaryByAllParams = true,
                VaryByCustom = "Hello"
            };

            var actualObj = RoundTrip(obj);
            Assert.Equal(obj, actualObj);
        }

        private T RoundTrip<T>(T obj) {
            return (T)BinarySerializer.Deserialize(BinarySerializer.Serialize(obj));
        }
    }
}
