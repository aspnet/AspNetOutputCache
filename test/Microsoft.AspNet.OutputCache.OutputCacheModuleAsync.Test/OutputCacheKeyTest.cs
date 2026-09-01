// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.Test {
    using Xunit;

    public class OutputCacheKeyTest {
        [Fact]
        public void Writer_Is_Deterministic_And_Distinguishes_Field_States() {
            var first = CreateKey("name", null);
            var same = CreateKey("name", null);
            var empty = CreateKey("name", string.Empty);
            var sentinel = CreateKey("name", "+n+");

            Assert.Equal(first.HashedKey, same.HashedKey);
            Assert.Equal(first.CanonicalId, same.CanonicalId);
            Assert.NotEqual(first.HashedKey, empty.HashedKey);
            Assert.NotEqual(first.HashedKey, sentinel.HashedKey);
            Assert.NotEqual(empty.HashedKey, sentinel.HashedKey);
        }

        [Fact]
        public void Writer_Accepts_CanonicalId_At_Size_Limit() {
            OutputCacheKey key;
            var writer = new OutputCacheKeyWriter();
            writer.WriteString(new string('x', 8183));
            Assert.True(writer.TryGetKey(out key));

            Assert.Equal(
                OutputCacheKeyWriter.MaxCanonicalIdLength,
                key.CanonicalId.Length);
        }

        [Fact]
        public void Writer_Rejects_CanonicalId_Above_Size_Limit() {
            OutputCacheKey key;
            var writer = new OutputCacheKeyWriter();
            writer.WriteString(new string('x', 8184));
            Assert.False(writer.TryGetKey(out key));

            Assert.Null(key);
        }

        [Fact]
        public void Writer_Produces_Compact_Length_Framed_Text() {
            var writer = new OutputCacheKeyWriter();
            writer.WriteString("a:b");
            writer.WriteString(null);
            writer.WriteString(string.Empty);
            OutputCacheKey key;

            Assert.True(writer.TryGetKey(out key));
            Assert.Equal("OC2S3:a:bNS0:", key.CanonicalId);
        }

        [Fact]
        public void Hash_Distinguishes_All_Utf16_Code_Units() {
            var first = CreateKey("\ud800", null);
            var second = CreateKey("\ud801", null);

            Assert.NotEqual(first.CanonicalId, second.CanonicalId);
            Assert.NotEqual(first.HashedKey, second.HashedKey);
        }

        private static OutputCacheKey CreateKey(string name, string value) {
            var writer = new OutputCacheKeyWriter();
            writer.WriteString(name);
            writer.WriteString(value);
            OutputCacheKey key;
            Assert.True(writer.TryGetKey(out key));
            return key;
        }
    }
}
