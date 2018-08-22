// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;
    using Moq;
    using System.Collections.Specialized;

    public class CosmosDBTableAsyncOutputCacheProviderTest
    {
        private const string ProviderName = "MyCosmosDBTableAsyncOutputCacheProvider";
        private static readonly NameValueCollection TestConfig = new NameValueCollection();
        private const string TestKey = "testkey";
        private static readonly object TestEntry = new object();
        private static readonly DateTime TestExpiry = DateTime.UtcNow.AddSeconds(100);

        [Fact]
        public void Add_Should_Return_Result_From_Repository_Add()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            repoMoq.Setup(repo => repo.Add(TestKey, TestEntry, TestExpiry)).Returns(TestEntry);
            var provider = CreateAndInitProvider(repoMoq.Object);

            var addedEntry = provider.Add(TestKey, TestEntry, TestExpiry);
            Assert.Equal(TestEntry, addedEntry);
        }

        [Fact]
        public async void AddAsync_Should_Return_Result_From_Repository_AddAsync()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            repoMoq.Setup(repo => repo.AddAsync(TestKey, TestEntry, TestExpiry))
                .Returns(Task.FromResult(TestEntry));
            var provider = CreateAndInitProvider(repoMoq.Object);

            var addedEntry = await provider.AddAsync(TestKey, TestEntry, TestExpiry);
            Assert.Equal(TestEntry, addedEntry);
        }

        [Fact]
        public void Get_Should_Return_Result_From_Repository_Get()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            repoMoq.Setup(repo => repo.Get(TestKey)).Returns(TestEntry);
            var provider = CreateAndInitProvider(repoMoq.Object);

            var result = provider.Get(TestKey);
            Assert.Equal(TestEntry, result);
        }

        [Fact]
        public async void GetAsync_Should_Return_Result_From_Repository_GetAsync()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            repoMoq.Setup(repo => repo.GetAsync(TestKey)).Returns(Task.FromResult(TestEntry));
            var provider = CreateAndInitProvider(repoMoq.Object);

            var result = await provider.GetAsync(TestKey);
            Assert.Equal(TestEntry, result);
        }

        [Fact]
        public void Remove_Should_Call_Repository_Remove()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            var removeCalled = false;
            repoMoq.Setup(repo => repo.Remove(TestKey)).Callback(() => removeCalled = true);
            var provider = CreateAndInitProvider(repoMoq.Object);

            provider.Remove(TestKey);
            Assert.True(removeCalled);
        }

        [Fact]
        public async void RemoveAsync_Should_Call_Repository_RemoveAsync()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            var removeAsyncCalled = false;
            repoMoq.Setup(repo => repo.RemoveAsync(TestKey)).Returns(Task.CompletedTask)
                .Callback(() => removeAsyncCalled = true);
            var provider = CreateAndInitProvider(repoMoq.Object);

            await provider.RemoveAsync(TestKey);
            Assert.True(removeAsyncCalled);
        }

        [Fact]
        public void Set_Should_Call_Repository_Set()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            var setCalled = false;
            repoMoq.Setup(repo => repo.Set(TestKey, TestEntry, TestExpiry)).Callback(() => setCalled = true);
            var provider = CreateAndInitProvider(repoMoq.Object);

            provider.Set(TestKey, TestEntry, TestExpiry);
            Assert.True(setCalled);
        }

        [Fact]
        public async void SetAsync_Should_Call_Repository_SetAsync()
        {
            var repoMoq = new Mock<ITableOutputCacheRepository>();
            var setAsyncCalled = false;
            repoMoq.Setup(repo => repo.SetAsync(TestKey, TestEntry, TestExpiry)).Returns(Task.CompletedTask)
                .Callback(() => setAsyncCalled = true);
            var provider = CreateAndInitProvider(repoMoq.Object);

            await provider.SetAsync(TestKey, TestEntry, TestExpiry);
            Assert.True(setAsyncCalled);
        }

        private CosmosDBTableAsyncOutputCacheProvider CreateAndInitProvider(ITableOutputCacheRepository repo)
        {
            var provider = new CosmosDBTableAsyncOutputCacheProvider();
            provider.Initialize(ProviderName, TestConfig, repo);

            return provider;
        }
    }
}
