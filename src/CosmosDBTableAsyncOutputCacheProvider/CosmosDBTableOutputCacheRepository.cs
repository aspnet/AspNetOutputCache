// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider {
    using System;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Runtime.Caching;
    using System.Threading.Tasks;
    using System.Web;
    using Azure;
    using Azure.Data.Tables;
    using Resource;

    class CosmosDBTableOutputCacheRepository : ITableOutputCacheRepository {
        private const string TableNameKey = "tableName";
        private const string ConnectionStringKey = "connectionStringName";
        private const string FixedPartitionKey = "P";

        private TableClient _tableClient;
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly object _lock = new object();

        public CosmosDBTableOutputCacheRepository(NameValueCollection providerConfig, NameValueCollection appSettings) {
            var connectionStringName = providerConfig[ConnectionStringKey];
            if (string.IsNullOrEmpty(connectionStringName)) {
                throw new ConfigurationErrorsException(SR.Cant_find_connectionStringName);
            }

            _connectionString = appSettings[connectionStringName];
            if (string.IsNullOrEmpty(_connectionString)) {
                throw new ConfigurationErrorsException(string.Format(SR.Cant_find_connectionString, connectionStringName));
            }

            _tableName = providerConfig[TableNameKey];
            if (string.IsNullOrEmpty(_tableName)) {
                throw new ConfigurationErrorsException(SR.TableName_cant_be_empty);
            }
        }

        public object Add(string key, object entry, DateTime utcExpiry) {
            CacheEntity existingCacheEntry = Get(key) as CacheEntity;

            if (existingCacheEntry != null && existingCacheEntry.UtcExpiry > DateTime.UtcNow) {
                return existingCacheEntry.CacheItem;
            } else {
                Set(key, entry, utcExpiry);
                return entry;
            }
        }

        public async Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            // If there is already a value in the cache for the specified key, the provider must return that value if not expired 
            // and must not store the data passed by using the Add method parameters. 
            CacheEntity existingCacheEntry = await GetAsync(key) as CacheEntity;

            if (existingCacheEntry != null && existingCacheEntry.UtcExpiry > DateTime.UtcNow) {
                return existingCacheEntry.CacheItem;
            } else {
                await SetAsync(key, entry, utcExpiry);
                return entry;
            }
        }

        public object Get(string key) {
            try
            {
                CacheEntity existingCacheEntry = _tableClient.GetEntity<CacheEntity>(CacheEntity.GeneratePartitionKey(key), CacheEntity.SanitizeKey(key));

                if (existingCacheEntry != null && existingCacheEntry.UtcExpiry < DateTime.UtcNow) {
                    Remove(key);
                    return null;
                } else {
                    return existingCacheEntry?.CacheItem;
                }
            }
            catch (RequestFailedException rfe) when (rfe.Status == 404) { /* Entity not found */ }
            return null;
        }

        public async Task<object> GetAsync(string key) {

            try
            {
                // Outputcache module will always first call GetAsync
                // so only calling EnsureTableInitializedAsync here is good enough
                await EnsureTableInitializedAsync();

                CacheEntity existingCacheEntry = await _tableClient.GetEntityAsync<CacheEntity>(CacheEntity.GeneratePartitionKey(key), CacheEntity.SanitizeKey(key));

                if (existingCacheEntry != null && existingCacheEntry.UtcExpiry < DateTime.UtcNow) {
                    await RemoveAsync(key);
                    return null;
                } else {
                    return existingCacheEntry?.CacheItem;
                }
            }
            catch (RequestFailedException rfe) when (rfe.Status == 404) { /* Entity not found */ }
            return null;
        }

        public void Remove(string key) {
            _tableClient.DeleteEntity(key, null, ETag.All);
        }

        public async Task RemoveAsync(string key) {
            await _tableClient.DeleteEntityAsync(key, null, ETag.All);
        }

        public void Set(string key, object entry, DateTime utcExpiry) {
            _tableClient.UpsertEntity<CacheEntity>(new CacheEntity(key, entry, utcExpiry));
        }

        public async Task SetAsync(string key, object entry, DateTime utcExpiry) {
            //Check if the key is already in database
            //If there is already a value in the cache for the specified key, the Set method will update it. 
            //Otherwise it will insert the entry.
            await _tableClient.UpsertEntityAsync<CacheEntity>(new CacheEntity(key, entry, utcExpiry));
        }

        private async Task EnsureTableInitializedAsync() {
            if (_tableClient != null) {
                return;
            }

            try {
                lock (_lock) {
                    if (_tableClient != null) {
                        return;
                    }

                    try {
                        _tableClient = new TableClient(_connectionString, _tableName);
                    } catch (FormatException) {
                        throw new HttpException(SR.Invalid_storage_account_information);
                    } catch (ArgumentException) {
                        throw new HttpException(SR.Invalid_storage_account_information);
                    }
                }

                await _tableClient.CreateIfNotExistsAsync();

            } catch (RequestFailedException ex) {
                throw new HttpException(SR.Fail_to_create_table, ex);
            }
        }
    }
}
