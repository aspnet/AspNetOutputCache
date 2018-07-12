using Microsoft.Azure.CosmosDB.Table;
using Microsoft.Azure.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider
{
    class CacheEntity : TableEntity
    {
        private static readonly char[] InvalidCharsInResource = {'/', '\\', '?', '#'};
        private const char ReplacementOfInvalidChars = '_';

        // This is required by TableQuery 
        public CacheEntity() { }

        public CacheEntity(string cacheKey, object cacheItem, DateTime utcExpiry)
        {
            RowKey = SanitizeKey(cacheKey);
            PartitionKey = GeneratePartitionKey(cacheKey);
            CacheItem = cacheItem;
            UtcExpiry= utcExpiry;
        }

        public object CacheItem { get; set; }

        public DateTime UtcExpiry { get; set; }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            CacheItem = Deserialize(properties[nameof(CacheItem)].BinaryValue);
        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var result = base.WriteEntity(operationContext);
            var cacheItemProperty = new EntityProperty(Serialize(CacheItem));
            result.Add(nameof(CacheItem), cacheItemProperty);
            return result;
        }

        public static string GeneratePartitionKey(string cacheKey)
        {
            return (cacheKey.Length % 10).ToString();
        }

        public static string SanitizeKey(string cacheKey)
        {
            // some chars are not allowed in rowkey
            // https://docs.microsoft.com/en-us/rest/api/storageservices/Understanding-the-Table-Service-Data-Model
            var sbKey = new StringBuilder(cacheKey);

            foreach(var c in InvalidCharsInResource)
            {
                sbKey.Replace(c, ReplacementOfInvalidChars);
            }
            return sbKey.ToString();
        }

        private static byte[] Serialize(object data)
        {
            if (data == null)
            {
                data = new object();
            }

            using (var memoryStream = new MemoryStream())
            {
                var binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(memoryStream, data);
                return memoryStream.ToArray();
            }
        }

        private static object Deserialize(byte[] data)
        {
            if (data == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream(data, 0, data.Length))
            {
                var binaryFormatter = new BinaryFormatter();
                return binaryFormatter.Deserialize(memoryStream);
            }
        }
    }
}
