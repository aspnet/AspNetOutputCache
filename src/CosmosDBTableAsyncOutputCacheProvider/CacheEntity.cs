// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider {
    using Azure;
    using Azure.Data.Tables;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text;

    class CacheEntity : ITableEntity {
        private static readonly char[] InvalidCharsInResource = { '/', '\\', '?', '#' };
        private const char ReplacementOfInvalidChars = '_';

        // This is required by TableQuery 
        public CacheEntity() { }

        public CacheEntity(string cacheKey, object cacheItem, DateTime utcExpiry) {
            RowKey = SanitizeKey(cacheKey);
            PartitionKey = GeneratePartitionKey(cacheKey);
            CacheItem = cacheItem;
            UtcExpiry = utcExpiry.ToUniversalTime();
        }

        [IgnoreDataMember]
        public object CacheItem { get; set; }
        [DataMember(Name = "CacheItem")]
        public byte[] SerializedCacheItem
        {
            get => Serialize(CacheItem);
            set => CacheItem = Deserialize(value);
        }

        public DateTime UtcExpiry { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public static string GeneratePartitionKey(string cacheKey) {
            return (cacheKey.Length % 10).ToString();
        }

        public static string SanitizeKey(string cacheKey) {
            // some chars are not allowed in rowkey
            // https://docs.microsoft.com/en-us/rest/api/storageservices/Understanding-the-Table-Service-Data-Model
            var sbKey = new StringBuilder(cacheKey);

            foreach (var c in InvalidCharsInResource) {
                sbKey.Replace(c, ReplacementOfInvalidChars);
            }
            return sbKey.ToString();
        }

        private static byte[] Serialize(object data) {
            if (data == null) {
                data = new object();
            }

            using (var memoryStream = new MemoryStream()) {
                var binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, data);
                return memoryStream.ToArray();
            }
        }

        private static object Deserialize(byte[] data) {
            if (data == null) {
                return null;
            }

            using (var memoryStream = new MemoryStream(data, 0, data.Length)) {
                var binaryFormatter = new BinaryFormatter();
                return binaryFormatter.Deserialize(memoryStream);
            }
        }
    }
}
