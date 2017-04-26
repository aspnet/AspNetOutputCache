namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Collections.Specialized;
    using System.Configuration;

    class SQLHelper {
        public ConnectionStringSettings ConnectionStringInfo { get; set; }
        private const string InMemoryTableConfigurationName = "UseInMemoryTable";

        #region CreateSessionTable
        // Premium database on a V12 server is required for InMemoryTable
        // DB owner needs to ALTER DATABASE [Database Name] SET MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT=ON;
        // Most of the SQL statement should just work, the following statements are different        
        private static readonly string CreateInMemoryOutputCacheTableSql = $@"
        IF NOT EXISTS (SELECT * 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = '{SqlOutputCacheParameters.TableName}')   
                CREATE TABLE {SqlOutputCacheParameters.TableName} (
                [Id] UNIQUEIDENTIFIER DEFAULT (newid()) NOT NULL,
                [Key] NVARCHAR(MAX) NOT NULL,
                [Value] VARBINARY(MAX) NULL,
                [UtcExpiry] DATETIME NULL, 
                PRIMARY KEY NONCLUSTERED 
                ([Id] ASC))WITH(MEMORY_OPTIMIZED=ON, DURABILITY=SCHEMA_ONLY)";
        private static readonly string CreateOutputCacheTableSql = $@"
                IF NOT EXISTS (SELECT * 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = '{SqlOutputCacheParameters.TableName}')    
                CREATE TABLE {SqlOutputCacheParameters.TableName} (
                [Id] UNIQUEIDENTIFIER DEFAULT (newid()) NOT NULL,
                [Key] NVARCHAR(MAX) NOT NULL,
                [Value] VARBINARY(MAX) NULL,
                [UtcExpiry] DATETIME NULL, 
                PRIMARY KEY NONCLUSTERED 
                ([Id] ASC))";
        #endregion

        public SQLHelper(NameValueCollection config) {
            ConnectionStringInfo = new ConnectionStringSettings(config["connectionStringName"], ConfigurationManager.ConnectionStrings[config["connectionStringName"]].ConnectionString);
            var useInMemoryTable = false;
            if (config[InMemoryTableConfigurationName] != null) {
                if (bool.TryParse(config[InMemoryTableConfigurationName], out useInMemoryTable) && useInMemoryTable) {
                    CreatTableIfNotExists(CreateInMemoryOutputCacheTableSql);
                }
                else {
                    CreatTableIfNotExists(CreateOutputCacheTableSql);
                }
                config.Remove(InMemoryTableConfigurationName);
            }
        }

        public async Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            //If there is already a value in the cache for the specified key, the provider must return that value. The provider must not store the data passed by using the Add method parameters. 
            if (await DoesKeyExist(key)) {
                var value = await GetNonExpiredEntry(key);
                if (value != null)
                    return value;
                else {
                    await RemoveAsync(key);
                }
            }
            // The Add method stores the data if it is not already in the cache.
            // We will insert the new value even where there was a key but with value expired or with value of null 
            return await InsertEntryAsync(key, entry, utcExpiry);
        }

        public async Task<object> GetAsync(string key) {
            if (await DoesKeyExist(key)) {
                var value = await GetNonExpiredEntry(key);
                if (value != null)
                    return value;
                else {
                    await RemoveEntryAsync(key);
                }
            }
            return null;
        }

        public async Task SetAsync(string key, object entry, DateTime utcExpiry) {
            //Check if the key is already in database
            //If there is already a value in the cache for the specified key, the Set method will update it. 
            //Otherwise it will insert the entry.
            if (await DoesKeyExist(key)) {
                await UpdateEntryAsync(key, entry, utcExpiry);
            }
            else {
                await InsertEntryAsync(key, entry, utcExpiry);
            }
        }

        public async Task RemoveAsync(string key) {
            await RemoveEntryAsync(key);
        }

        private async Task RemoveEntryAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"DELETE FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                await RunQuery(cmd);
            }
        }

        async Task<bool> DoesKeyExist(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT [Key] FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (cmd.Connection = await GetConn()) {
                    using (var reader = await cmd.ExecuteReaderAsync()) {
                        if (await reader.ReadAsync()) {
                            return true;
                        }
                        return false;
                    }
                }
            }
        }
        async Task UpdateEntryAsync(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"UPDATE {SqlOutputCacheParameters.TableName} SET [Value] = @value,[UtcExpiry]=@utcExpiry WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                await RunQuery(cmd);
            }
        }
        async Task<object> InsertEntryAsync(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"INSERT INTO {SqlOutputCacheParameters.TableName} ([Key], [Value], [UtcExpiry]) VALUES (@key, @value, @utcExpiry)";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                await RunQuery(cmd);
                return entry;
            }
        }

        async Task<object> GetNonExpiredEntry(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT * FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (cmd.Connection = await GetConn()) {
                    using (var reader = await cmd.ExecuteReaderAsync()) {
                        if (await reader.ReadAsync()) {
                            if ((DateTime)reader["UtcExpiry"] > DateTime.Now.ToUniversalTime()) {
                                return BinarySerializer.Deserialize((byte[])reader["Value"]);
                            }
                        }
                    }
                }
            }
            return null;
        }
        void CreatTableIfNotExists(string CreateTableSql) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = CreateTableSql;
                using (var conn = new SqlConnection(ConnectionStringInfo.ConnectionString)) {
                    conn.Open();
                    cmd.Connection = conn;
                    cmd.ExecuteScalar();
                }
            }
        }

        async Task<SqlConnection> GetConn() {
            var conn = new SqlConnection(ConnectionStringInfo.ConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        async Task RunQuery(SqlCommand cmd) {
            using (cmd.Connection = await GetConn()) {
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public class SqlOutputCacheParameters {
        public static readonly string TableName = "OutputCacheAsync";
    }
}