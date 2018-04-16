// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider {
    using System;
    using System.Threading.Tasks;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Data;
    using System.Security.Principal;
    using System.Web;
    using Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider.Resource;

    class SqlOutputCacheRepository : ISqlOutputCacheRepository {
        #region Private fields
        private const int SQL_LOGIN_FAILED = 18456;
        private const int SQL_LOGIN_FAILED_2 = 18452;
        private const int SQL_LOGIN_FAILED_3 = 18450;

        private const string TableName = "OutputCacheAsync";
        private const string InMemoryTableConfigurationName = "UseInMemoryTable";
        #endregion

        #region SQL Commands to Create OutputCache Table
        // Premium database on a V12 server is required for InMemoryTable
        // DB owner needs to ALTER DATABASE [Database Name] SET MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT=ON;
        // Most of the SQL statement should just work, the following statements are different        
        private static readonly string CreateInMemoryOutputCacheTableSql = $@"
        IF NOT EXISTS (SELECT * 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = '{TableName}')   
                CREATE TABLE {TableName} (
                [Id] UNIQUEIDENTIFIER DEFAULT (newid()) NOT NULL,
                [Key] NVARCHAR(MAX) NOT NULL,
                [Value] VARBINARY(MAX) NULL,
                [UtcExpiry] DATETIME NULL, 
                PRIMARY KEY NONCLUSTERED 
                ([Id] ASC))WITH(MEMORY_OPTIMIZED=ON, DURABILITY=SCHEMA_ONLY)";
        private static readonly string CreateOutputCacheTableSql = $@"
                IF NOT EXISTS (SELECT * 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_NAME = '{TableName}')    
                CREATE TABLE {TableName} (
                [Id] UNIQUEIDENTIFIER DEFAULT (newid()) NOT NULL,
                [Key] NVARCHAR(MAX) NOT NULL,
                [Value] VARBINARY(MAX) NULL,
                [UtcExpiry] DATETIME NULL, 
                PRIMARY KEY NONCLUSTERED 
                ([Id] ASC))";
        #endregion

        #region Constructor
        public SqlOutputCacheRepository(NameValueCollection config) : this(config, true) { }
        #endregion

        #region For unit tests
        internal SqlOutputCacheRepository(NameValueCollection config, bool createDb) {
            var connectionStrName = config["connectionStringName"];
            if (string.IsNullOrEmpty(connectionStrName)) {
                throw new ConfigurationErrorsException(SR.Cant_find_connectionStringName);
            }

            ConnectionString = GetConnectString(connectionStrName);
            if (string.IsNullOrEmpty(ConnectionString)) {
                throw new ConfigurationErrorsException(string.Format(SR.Cant_find_connectionString, connectionStrName));
            }

            var useInMemoryTable = false;
            if (bool.TryParse(config[InMemoryTableConfigurationName], out useInMemoryTable) && useInMemoryTable) {
                IsUsingInMemoryTable = true;
            }

            if (createDb) {
                var sql = IsUsingInMemoryTable ? CreateInMemoryOutputCacheTableSql : CreateOutputCacheTableSql;
                CreateTableIfNotExists(sql);
            }

            config.Remove(InMemoryTableConfigurationName);
        }

        internal string ConnectionString { get; set; }

        internal bool IsUsingInMemoryTable { get; private set; }

        internal static Func<string, string> GetConnectString = 
            (connectionStrName) => ConfigurationManager.ConnectionStrings[connectionStrName]?.ConnectionString;
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
        
        #region private Async Methods
        private async Task RemoveEntryAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"DELETE FROM {TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (var connection = new SqlConnection(ConnectionString)) {
                    await SqlExecuteNonQueryAsync(connection, cmd);
                }
            }
        }

        private async Task<bool> DoesKeyExistAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT [Key] FROM {TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (var connection = new SqlConnection(ConnectionString)) {
                    using (var reader = await SqlExecuteReaderAsync(connection, cmd)) {
                        if (await reader.ReadAsync()) {
                            return true;
                        }
                        return false;
                    }
                }
            }
        }

        private async Task UpdateEntryAsync(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"UPDATE {TableName} SET [Value] = @value,[UtcExpiry]=@utcExpiry WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                using (var connection = new SqlConnection(ConnectionString)) {
                    await SqlExecuteNonQueryAsync(connection, cmd);
                }
            }
        }

        private async Task<object> InsertEntryAsync(string key, object entry, DateTime utcExpiry) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"INSERT INTO {TableName} ([Key], [Value], [UtcExpiry]) VALUES (@key, @value, @utcExpiry)";
                cmd.Parameters.AddWithValue("key", key);
                cmd.Parameters.AddWithValue("value", BinarySerializer.Serialize(entry));
                cmd.Parameters.AddWithValue("utcExpiry", utcExpiry.ToUniversalTime());
                using (var connection = new SqlConnection(ConnectionString)) {
                    await SqlExecuteNonQueryAsync(connection, cmd);
                }
                return entry;
            }
        }

        private async Task<object> GetNonExpiredEntryAsync(string key) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = $@"SELECT * FROM {TableName} WHERE [Key] = @key";
                cmd.Parameters.AddWithValue("key", key);
                using (var connection = new SqlConnection(ConnectionString)) {
                    using (var reader = await SqlExecuteReaderAsync(connection, cmd)) {
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

        private static async Task OpenConnectionAsync(SqlConnection sqlConnection) {
            try {
                if (sqlConnection.State != ConnectionState.Open) {
                    await sqlConnection.OpenAsync().ConfigureAwait(false);
                }
            } catch (SqlException e) {
                if (e != null &&
                    (e.Number == SQL_LOGIN_FAILED ||
                     e.Number == SQL_LOGIN_FAILED_2 ||
                     e.Number == SQL_LOGIN_FAILED_3)) {
                    string user;

                    SqlConnectionStringBuilder scsb = new SqlConnectionStringBuilder(sqlConnection.ConnectionString);
                    if (scsb.IntegratedSecurity) {
                        user = WindowsIdentity.GetCurrent().Name;
                    } else {
                        user = scsb.UserID;
                    }

                    throw new HttpException(string.Format(SR.Login_failed_sql_session_database, user), e);
                }
            } catch (Exception e) {
                // just throw, we have a different Exception
                throw new HttpException(SR.Cant_connect_sql_session_database, e);
            }
        }

        private static async Task<SqlDataReader> SqlExecuteReaderAsync(SqlConnection connection, SqlCommand sqlCmd) {
            sqlCmd.Connection = connection;

            await OpenConnectionAsync(connection).ConfigureAwait(false);
            return await sqlCmd.ExecuteReaderAsync().ConfigureAwait(false);
        }


        private static async Task SqlExecuteNonQueryAsync(SqlConnection connection, SqlCommand sqlCommand) {
            sqlCommand.Connection = connection;

            await OpenConnectionAsync(connection).ConfigureAwait(false);
            await sqlCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        #endregion


        #region private Sync Methods
        private object GetNonExpiredEntry(string key) {
            return GetNonExpiredEntryAsync(key).GetAwaiter().GetResult();
        }

        private object InsertEntry(string key, object entry, DateTime utcExpiry) {
            return InsertEntryAsync(key, entry, utcExpiry).GetAwaiter().GetResult();
        }

        private bool DoesKeyExist(string key) {
            return DoesKeyExistAsync(key).GetAwaiter().GetResult();
        }

        private void RemoveEntry(string key) {
            RemoveEntryAsync(key).GetAwaiter().GetResult();
        }

        private void UpdateEntry(string key, object entry, DateTime utcExpiry) {
            UpdateEntryAsync(key, entry, utcExpiry).GetAwaiter().GetResult();
        }

        private void CreateTableIfNotExists(string createTableSql) {
            using (var cmd = new SqlCommand()) {
                cmd.CommandText = createTableSql;
                using (var connection = new SqlConnection(ConnectionString)) {
                    SqlExecuteNonQueryAsync(connection, cmd).GetAwaiter().GetResult();
                }
            }
        }
        #endregion
    }
}