// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache {
    using System;
    using System.Globalization;
    using System.Text;

    sealed class OutputCacheKey {
        private const string HashedKeyPrefix = "oc:v2:";

        public OutputCacheKey(string canonicalId) {
            CanonicalId = canonicalId;
            HashedKey = HashedKeyPrefix + ToBase64Url(ComputeHash(canonicalId));
        }

        public string CanonicalId { get; private set; }

        public string HashedKey { get; private set; }

        private static byte[] ComputeHash(string value) {
            var bytes = new byte[value.Length * sizeof(char)];
            for (int i = 0; i < value.Length; i++) {
                bytes[i * 2] = (byte)value[i];
                bytes[(i * 2) + 1] = (byte)(value[i] >> 8);
            }
            return CryptoUtil.ComputeHash(bytes);
        }

        private static string ToBase64Url(byte[] value) {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }

    [Serializable]
    sealed class VerifiedCacheEntry {
        public VerifiedCacheEntry(string canonicalId, object cacheValue) {
            CanonicalId = canonicalId;
            CacheValue = cacheValue;
        }

        public string CanonicalId { get; private set; }

        public object CacheValue { get; private set; }
    }

    sealed class OutputCacheKeyWriter {
        public const int MaxCanonicalIdLength = 8192;

        private readonly StringBuilder _builder = new StringBuilder("OC2");
        private bool _tooLong;

        public bool TryGetKey(out OutputCacheKey key) {
            if (_tooLong) {
                key = null;
                return false;
            }

            key = new OutputCacheKey(_builder.ToString());
            return true;
        }

        public void WriteToken(char value) {
            Append(value.ToString());
        }

        public void WriteArrayLength(int value) {
            if (value < 0) {
                Append("N");
            }
            else {
                AppendLength('A', value);
            }
        }

        public void WriteString(string value) {
            if (value == null) {
                Append("N");
                return;
            }

            AppendLength('S', value.Length);
            Append(value);
        }

        public void WriteBytes(byte[] value) {
            if (value == null) {
                Append("N");
                return;
            }

            string base64Value = Convert.ToBase64String(value);
            AppendLength('B', base64Value.Length);
            Append(base64Value);
        }

        private void AppendLength(char type, int length) {
            Append(type + length.ToString(CultureInfo.InvariantCulture) + ":");
        }

        private void Append(string value) {
            if (_tooLong || _builder.Length + value.Length > MaxCanonicalIdLength) {
                _tooLong = true;
                return;
            }

            _builder.Append(value);
        }
    }
}
