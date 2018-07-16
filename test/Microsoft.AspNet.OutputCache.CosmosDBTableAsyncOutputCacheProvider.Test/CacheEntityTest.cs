// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider.Test
{
    using System;
    using Xunit;

    public class CacheEntityTest
    {
        [Fact]
        public void SanitizeKey_Should_Replace_Invalid_Chars_In_Key()
        {
            // invalid chars: '/', '\\', '?', '#'
            var sanitizedKey = CacheEntity.SanitizeKey("/k\\e?y#");
            Assert.Equal("_k_e_y_", sanitizedKey);

            sanitizedKey = CacheEntity.SanitizeKey("_key_/\\?#");
            Assert.Equal("_key_____", sanitizedKey);

            sanitizedKey = CacheEntity.SanitizeKey("#\\k?#e/\\y?/");
            Assert.Equal("__k__e__y__", sanitizedKey);
        }

        [Fact]
        public void GeneratePartitionKey_Should_Generate_PartitionKey_Based_On_Key_Length()
        {
            var pkey = CacheEntity.GeneratePartitionKey("");
            Assert.Equal("0", pkey);

            pkey = CacheEntity.GeneratePartitionKey("a");
            Assert.Equal("1", pkey);

            pkey = CacheEntity.GeneratePartitionKey("123456789");
            Assert.Equal("9", pkey);

            pkey = CacheEntity.GeneratePartitionKey("1234567890");
            Assert.Equal("0", pkey);

            pkey = CacheEntity.GeneratePartitionKey("123456789012");
            Assert.Equal("2", pkey);
        }

        [Fact]
        public void Constructor_Should_Initialize_Properties_Correctly()
        {
            var cacheItem = new object();
            var expiry = DateTime.UtcNow.AddMinutes(10);
            var ce = new CacheEntity("/webform.aspxac", cacheItem, expiry);

            Assert.Equal("_webform.aspxac", ce.RowKey);
            Assert.Equal(cacheItem, ce.CacheItem);
            Assert.Equal("5", ce.PartitionKey);
            Assert.Equal(expiry, ce.UtcExpiry);
        }
    }
}
