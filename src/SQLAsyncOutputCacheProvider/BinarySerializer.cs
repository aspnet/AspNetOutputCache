// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;

    static class BinarySerializer {
        public static byte[] Serialize(object data) {
            if (data == null) {
                data = new object();
            }
            var binaryFormatter = new BinaryFormatter();
            using (var memoryStream = new MemoryStream()) {
                binaryFormatter.Serialize(memoryStream, data);
                byte[] objectDataAsStream = memoryStream.ToArray();
                return objectDataAsStream;
            }
        }

        public static object Deserialize(byte[] data) {
            if (data == null) {
                return null;
            }
            var binaryFormatter = new BinaryFormatter();
            using (var memoryStream = new MemoryStream(data, 0, data.Length)) {
                memoryStream.Seek(0, SeekOrigin.Begin);
                object retObject = (object)binaryFormatter.Deserialize(memoryStream);
                return retObject;
            }
        }
    }
}
