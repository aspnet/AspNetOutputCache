using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;
using Xunit;

namespace Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.Test
{
    public class InMemoryOutputCacheProviderTest
    {
        private const string TestKey1 = "key1";
        private const string TestKey2 = "key2";
        private static readonly object CacheVal1 = new object();
        private static readonly object CacheVal2 = new object ();
        private static readonly object CacheVal3 = new object();
        private static readonly object CacheVal4 = new object();
        private static readonly DateTime Expiry1 = DateTime.UtcNow.AddHours(1);
        private static readonly DateTime Expiry2 = Cache.NoAbsoluteExpiration;
        private static readonly CacheItemPolicy Policy1 = new CacheItemPolicy();
        private static readonly CacheItemPolicy Policy2 = new CacheItemPolicy();

        [Fact]
        public async void AddAsync_Should_Add_Item_To_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var test1Added = false;
            var test2Added = false;
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey1, CacheVal1, Expiry1, null))
                .Returns(null).Callback(() => test1Added = true);
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey2, CacheVal2, ObjectCache.InfiniteAbsoluteExpiration, null))
                .Returns(CacheVal3).Callback(() => test2Added = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            var r1 = await outputCache.AddAsync(TestKey1, CacheVal1, Expiry1);
            Assert.Null(r1);
            Assert.True(test1Added);

            var r2 = await outputCache.AddAsync(TestKey2, CacheVal2, Expiry2);
            Assert.Equal(CacheVal3, r2);
            Assert.True(test2Added);
        }

        [Fact]
        public async void AddAsync_Through_CacheItemPolicy_Should_Add_Item_To_Cache_If_Not_In_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var test1Added = false;
            var test2Added = false;
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey1, CacheVal1, Policy1, null))
                .Returns(null).Callback(() => test1Added = true);
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey2, CacheVal2, Policy2, null))
                .Returns(null).Callback(() => test2Added = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            var r1 = await ((ICacheDependencyHandler)outputCache).AddAsync(TestKey1, CacheVal1, Policy1);
            Assert.Null(r1);
            Assert.True(test1Added);

            var r2 = await ((ICacheDependencyHandler)outputCache).AddAsync(TestKey2, CacheVal2, Policy2);
            Assert.Null(r2);
            Assert.True(test2Added);
        }

        [Fact]
        public async void AddAsync_Through_CacheItemPolicy_Should_Return_Existing_Item_If_Its_Already_In_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey1, CacheVal1, Policy1, null))
                .Returns(CacheVal3);
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey2, CacheVal2, Policy2, null))
                .Returns(CacheVal4);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            var r1 = await ((ICacheDependencyHandler)outputCache).AddAsync(TestKey1, CacheVal1, Policy1);
            Assert.Equal(CacheVal3, r1);

            var r2 = await ((ICacheDependencyHandler)outputCache).AddAsync(TestKey2, CacheVal2, Policy2);
            Assert.Equal(CacheVal4, r2);
        }

        [Fact]
        public async void SetAsync_Through_CacheItemPolicy_Should_Insert_Item_To_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var test1Added = false;
            cacheMoq.Setup(cache => cache.Set(TestKey1, CacheVal1, Policy1, null))
                .Callback(() => test1Added = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            await ((ICacheDependencyHandler)outputCache).SetAsync(TestKey1, CacheVal1, Policy1);
            Assert.True(test1Added);
        }

        [Fact]
        public async void GetAsync_Should_Get_CacheItem_From_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            cacheMoq.Setup(cache => cache.Get(TestKey1, null)).Returns(CacheVal1);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            var r1 = await outputCache.GetAsync(TestKey1);
            Assert.Equal(CacheVal1, r1);
            var r2 = await outputCache.GetAsync(TestKey2);
            Assert.Null(r2);
        }

        [Fact]
        public async void SetAsync_Should_Replace_CacheItem_In_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var test1Set = false;
            var test2Set = false;

            cacheMoq.Setup(cache => cache.Set(TestKey1, CacheVal1, Expiry1, null))
                .Callback(() => test1Set = true);
            cacheMoq.Setup(cache => cache.Set(TestKey2, CacheVal2, ObjectCache.InfiniteAbsoluteExpiration, null))
                .Callback(() => test2Set = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            await outputCache.SetAsync(TestKey1, CacheVal1, Expiry1);
            Assert.True(test1Set);

            await outputCache.SetAsync(TestKey2, CacheVal2, Expiry2);
            Assert.True(test2Set);
        }

        [Fact]
        public async void RemoveAsync_Should_Remove_CacheItem_From_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var removed = false;
            cacheMoq.Setup(cache => cache.Remove(TestKey1, null)).Callback(()=> removed = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            await outputCache.RemoveAsync(TestKey1);
            Assert.True(removed);
        }

        [Fact]
        public void Get_Should_Get_CacheItem_From_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            cacheMoq.Setup(cache => cache.Get(TestKey1, null)).Returns(CacheVal1);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            var r1 = outputCache.Get(TestKey1);
            Assert.Equal(CacheVal1, r1);
            var r2 = outputCache.Get(TestKey2);
            Assert.Null(r2);
        }

        [Fact]
        public void Add_Should_Add_Item_To_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var test1Added = false;
            var test2Added = false;
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey1, CacheVal1, Expiry1, null))
                .Returns(null).Callback(() => test1Added = true);
            cacheMoq.Setup(cache => cache.AddOrGetExisting(TestKey2, CacheVal2, ObjectCache.InfiniteAbsoluteExpiration, null))
                .Returns(CacheVal3).Callback(() => test2Added = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            var r1 = outputCache.Add(TestKey1, CacheVal1, Expiry1);
            Assert.Null(r1);
            Assert.True(test1Added);

            var r2 = outputCache.Add(TestKey2, CacheVal2, Expiry2);
            Assert.Equal(CacheVal3, r2);
            Assert.True(test2Added);
        }

        [Fact]
        public void Set_Should_Replace_CacheItem_In_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var test1Added = false;
            var test2Added = false;
            cacheMoq.Setup(cache => cache.Set(TestKey1, CacheVal1, Expiry1, null))
                .Callback(() => test1Added = true);
            cacheMoq.Setup(cache => cache.Set(TestKey2, CacheVal2, ObjectCache.InfiniteAbsoluteExpiration, null))
                .Callback(() => test2Added = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            outputCache.Set(TestKey1, CacheVal1, Expiry1);
            Assert.True(test1Added);

            outputCache.Set(TestKey2, CacheVal2, Expiry2);
            Assert.True(test2Added);
        }

        [Fact]
        public void Remove_Should_Remove_CacheItem_From_Cache()
        {
            var cacheMoq = new Mock<ObjectCache>();
            var removed = false;
            cacheMoq.Setup(cache => cache.Remove(TestKey1, null)).Callback(() => removed = true);

            InMemoryOutputCacheProvider.InternalCache = cacheMoq.Object;

            var outputCache = new InMemoryOutputCacheProvider();
            outputCache.RemoveAsync(TestKey1);
            Assert.True(removed);
        }
    }
}
