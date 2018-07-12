

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider
{
    using System;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.CosmosDB.Table;
    using Microsoft.Azure.Storage;
    using Resource;

    public class CosmosDBTableOutputCacheRepository : ITableOutputCacheRepository
    {
        private const string TableNameKey = "tableName";
        private const string ConnectionStringKey = "connectionStringName";
        private const string FixedPartitionKey = "P";

        private CloudTable _table;
        private string _connectionString;
        private string _tableName;
        private bool _initialized = false;
        private object _lock = new object();
        

        public CosmosDBTableOutputCacheRepository(NameValueCollection providerConfig, NameValueCollection appSettings)
        {
            var connectionStringName = providerConfig[ConnectionStringKey];
            if (string.IsNullOrEmpty(connectionStringName))
            {
                throw new ConfigurationErrorsException(SR.Cant_find_connectionStringName);
            }

            _connectionString = appSettings[connectionStringName];
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new ConfigurationErrorsException(string.Format(SR.Cant_find_connectionString, connectionStringName));
            }

            _tableName = providerConfig[TableNameKey];
            if (string.IsNullOrEmpty(_tableName))
            {
                throw new ConfigurationErrorsException(SR.TableName_cant_be_empty);
            }
        }

        public object Add(string key, object entry, DateTime utcExpiry)
        {
            return AddAsync(key, entry, utcExpiry).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<object> AddAsync(string key, object entry, DateTime utcExpiry)
        {
            await EnsureTableInitializedAsync();
            // If there is already a value in the cache for the specified key, the provider must return that value if not expired 
            // and must not store the data passed by using the Add method parameters. 
            var retrieveOp = TableOperationWrapper.Retrieve(key);
            var retrieveResult = await _table.ExecuteAsync(retrieveOp);
            var existingCacheEntry = retrieveResult.Result as CacheEntity;

            if(existingCacheEntry != null && existingCacheEntry.UtcExpiry > DateTime.UtcNow)
            {
                return existingCacheEntry.CacheItem;
            }
            else
            {
                await SetAsync(key, entry, utcExpiry);
                return entry;
            }
        }

        public object Get(string key)
        {
            return GetAsync(key).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<object> GetAsync(string key)
        {
            await EnsureTableInitializedAsync();

            var retrieveOp = TableOperationWrapper.Retrieve(key);
            var retrieveResult = await _table.ExecuteAsync(retrieveOp);

            var existingCacheEntry = retrieveResult.Result as CacheEntity;

            if(existingCacheEntry != null && existingCacheEntry.UtcExpiry < DateTime.UtcNow){
                await RemoveAsync(key);
                return null;
            }
            else
            {
                return existingCacheEntry?.CacheItem;
            }
        }

        public void Remove(string key)
        {
            RemoveAsync(key).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task RemoveAsync(string key)
        {
            await EnsureTableInitializedAsync();

            var removeOp = TableOperationWrapper.Delete(key);
            
            await _table.ExecuteAsync(removeOp);
        }

        public void Set(string key, object entry, DateTime utcExpiry)
        {
            SetAsync(key, entry, utcExpiry).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task SetAsync(string key, object entry, DateTime utcExpiry)
        {
            await EnsureTableInitializedAsync();

            //Check if the key is already in database
            //If there is already a value in the cache for the specified key, the Set method will update it. 
            //Otherwise it will insert the entry.
            var insertOp = TableOperationWrapper.InsertOrReplace(key, entry, utcExpiry);
            await _table.ExecuteAsync(insertOp);            
        }

        private async Task EnsureTableInitializedAsync()
        {
            if(!_initialized)
            {
                lock (_lock)
                {
                    if(!_initialized)
                    {
                        var storageAccount = CreateStorageAccount();
                        var tableClient = storageAccount.CreateCloudTableClient();
                        _table = tableClient.GetTableReference(_tableName);
                    }
                }

                try
                {
                    await _table.CreateIfNotExistsAsync();
                }
                catch (StorageException ex)
                {
                    throw new HttpException(SR.Fail_to_create_table, ex);
                }
                _initialized = true;
            }
        }

        private CloudStorageAccount CreateStorageAccount()
        {
            try
            {
                return CloudStorageAccount.Parse(_connectionString);
            }
            catch (FormatException)
            {
                throw new HttpException(SR.Invalid_storage_account_information);
            }
            catch (ArgumentException)
            {
                throw new HttpException(SR.Invalid_storage_account_information);
            }
        }
    }
}
