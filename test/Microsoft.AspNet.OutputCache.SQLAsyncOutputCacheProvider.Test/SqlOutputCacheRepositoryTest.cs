using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider.Test {
    public class SqlOutputCacheRepositoryTest {
        private const string ConnectionStringNameKey = "connectionStringName";
        private const string TestConnectionStringName = "TestConnectionString";
        private const string TestConnectionString = "Data Source=ServerName;Initial Catalog=DatabaseName;User Id=userid;Password=password";
        private const string InMemoryTableConfigurationName = "UseInMemoryTable";

        static SqlOutputCacheRepositoryTest() {
            SqlOutputCacheRepository.GetConnectString = (name) => {
                if(name == TestConnectionStringName) {
                    return TestConnectionString;
                }
                return string.Empty;
            };
        }

        [Fact]
        public void Creating_SqlOutputCacheRepository_Should_Base_On_Configuration() {
            var useInMemoryTableConfig = new NameValueCollection();
            useInMemoryTableConfig.Add(ConnectionStringNameKey, TestConnectionStringName);
            useInMemoryTableConfig.Add(InMemoryTableConfigurationName, "true");
            var repo = new SqlOutputCacheRepository(useInMemoryTableConfig, false);

            Assert.True(repo.IsUsingInMemoryTable);
            Assert.Equal(TestConnectionString, repo.ConnectionString);

            var defaultConfig = new NameValueCollection();
            defaultConfig.Add(ConnectionStringNameKey, TestConnectionStringName);
            repo = new SqlOutputCacheRepository(useInMemoryTableConfig, false);

            Assert.False(repo.IsUsingInMemoryTable);
            Assert.Equal(TestConnectionString, repo.ConnectionString);
        }

        [Fact]
        public void Creating_SqlOutputCacheRepository_Should_Throw_ConfigurationErrorsException_If_ConnectionString_Is_Misconfigured() {
            var config = new NameValueCollection();
            Assert.Throws<ConfigurationErrorsException>(() => new SqlOutputCacheRepository(config, false));

            config.Add(ConnectionStringNameKey, "notExistedConnectionStringName");
            Assert.Throws<ConfigurationErrorsException>(() => new SqlOutputCacheRepository(config, false));
        }
    }
}
