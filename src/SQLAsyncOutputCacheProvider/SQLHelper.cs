namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    using System;
    using System.Threading.Tasks;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Data;

    class SQLHelper {
        #region Private fields
        ConnectionStringSettings ConnectionStringInfo { get; set; }
        private const string InMemoryTableConfigurationName = "UseInMemoryTable";
        #endregion

        #region SQL Commands to Create OutputCache Table
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

        #region Constructor
        public SQLHelper(NameValueCollection config) {
            ConnectionStringInfo = new ConnectionStringSettings(config["connectionStringName"], ConfigurationManager.ConnectionStrings[config["connectionStringName"]].ConnectionString);
            var useInMemoryTable = false;
            if (bool.TryParse(config[InMemoryTableConfigurationName], out useInMemoryTable) && useInMemoryTable) {
                CreatTableIfNotExists(CreateInMemoryOutputCacheTableSql);
            }
            else {
                CreatTableIfNotExists(CreateOutputCacheTableSql);
            }
            config.Remove(InMemoryTableConfigurationName);
        }
        #endregion

        #region Public Async Methods
        public async Task<object> AddAsync(string key, object entry, DateTime utcExpiry) {
            //If there is already a value in the cache for the specified key, the provider must return that value. The provider must not store the data passed by using the Add method parameters. 
            if (await DoesKeyExistAsync(key)) {
                var value = await GetNonExpiredEntryAsync(key);
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
            if (await DoesKeyExistAsync(key)) {
                var value = await GetNonExpiredEntryAsync(key);
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
            if (await DoesKeyExistAsync(key)) {
                await UpdateEntryAsync(key, entry, utcExpiry);
            }
            else {
                await InsertEntryAsync(key, entry, utcExpiry);
            }
        }

        public async Task RemoveAsync(string key) {
            await RemoveEntryAsync(key);
        }
        #endregion

        #region Public Sync Methods 
        public object Add(string key, object entry, DateTime utcExpiry) {
            //If there is already a value in the cache for the specified key, the provider must return that value. The provider must not store the data passed by using the Add method parameters. 
            if (DoesKeyExist(key)) {
                var value = GetNonExpiredEntry(key);
                if (value != null)
                    return value;
                else {
                    Remove(key);
                }
            }
            // The Add method stores the data if it is not already in the cache.
            // We will insert the new value even where there was a key but with value expired or with value of null 
            return InsertEntry(key, entry, utcExpiry);
        }

        public object Get(string key) {
            if (DoesKeyExist(key)) {
                var value = GetNonExpiredEntry(key);
                if (value != null)
                    return value;
                else {
                    RemoveEntry(key);
                }
            }
            return null;
        }

        public void Remove(string key) {
            RemoveEntry(key);
        }

        public void Set(string key, object entry, DateTime utcExpiry) {
            //Check if the key is already in database
            //If there is already a value in the cache for the specified key, the Set method will update it. 
            //Otherwise it will insert the entry.
            if (DoesKeyExist(key)) {
                UpdateEntry(key, entry, utcExpiry);
            }
            else {
                InsertEntry(key, entry, utcExpiry);
            }
        }
        #endregion

        #region private Sync Methods
        SqlConnection GetConn() {
            var conn = new SqlConnection(ConnectionStringInfo.ConnectionString);
            conn.Open();
            return conn;
        }

        object GetNonExpiredEntry(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT * FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (cmd.Connection = GetConn()) {
                    using (var reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            if ((DateTime)reader["UtcExpiry"] > DateTime.Now.ToUniversalTime()) {
                                return BinarySerializer.Deserialize((byte[])reader["Value"]);
                            }
                        }
                    }
                }
            }
            return null;
        }

        object InsertEntry(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"INSERT INTO {SqlOutputCacheParameters.TableName} ([Key], [Value], [UtcExpiry]) VALUES (@key, @value, @utcExpiry)";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                RunQuery(cmd);
                return entry;
            }
        }

        bool DoesKeyExist(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT [Key] FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (cmd.Connection = GetConn()) {
                    using (var reader = cmd.ExecuteReader()) {
                        if (reader.Read()) {
                            return true;
                        }
                        return false;
                    }
                }
            }
        }

        void RemoveEntry(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"DELETE FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                RunQuery(cmd);
            }
        }

        void UpdateEntry(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"UPDATE {SqlOutputCacheParameters.TableName} SET [Value] = @value,[UtcExpiry]=@utcExpiry WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                RunQuery(cmd);
            }
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

        void RunQuery(SqlCommand cmd) {
            using (cmd.Connection = GetConn()) {
                cmd.ExecuteNonQuery();
            }
        }
        #endregion

        #region private Async Methods
        async Task RemoveEntryAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"DELETE FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                await RunQueryAsync(cmd);
            }
        }

        async Task<bool> DoesKeyExistAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT [Key] FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (cmd.Connection = await GetConnAsync()) {
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
                await RunQueryAsync(cmd);
            }
        }

        async Task<object> InsertEntryAsync(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"INSERT INTO {SqlOutputCacheParameters.TableName} ([Key], [Value], [UtcExpiry]) VALUES (@key, @value, @utcExpiry)";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                await RunQueryAsync(cmd);
                return entry;
            }
        }

        async Task<object> GetNonExpiredEntryAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT * FROM {SqlOutputCacheParameters.TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (cmd.Connection = await GetConnAsync()) {
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

        async Task<SqlConnection> GetConnAsync() {
            var conn = new SqlConnection(ConnectionStringInfo.ConnectionString);
            await conn.OpenAsync();
            return conn;
        }

        async Task RunQueryAsync(SqlCommand cmd) {
            using (cmd.Connection = await GetConnAsync()) {
                await cmd.ExecuteNonQueryAsync();
            }
        }
        #endregion
    }

    class SqlOutputCacheParameters {
        public static readonly string TableName = "OutputCacheAsync";
    }
}