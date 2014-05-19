using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Web.SessionState;

namespace MemcachedSessionProvider.Tests
{
    [TestFixture]
    public class SessionCacheWithBackupTests
    {
        private SessionCacheWithBackup cache = SessionCacheWithBackup.Instance;
        private SessionKeyFormat _format = new SessionKeyFormat(null);
        private const string SessionId = "abc";
        private string _primaryKey = new SessionKeyFormat(null).GetPrimaryKey(SessionId);
        private string _backupKey = new SessionKeyFormat(null).GetBackupKey(SessionId); 

        [Test]
        public void StoreSessionTest()
        {
            cache.ResetMemcachedClient("sessionManagement/memcached");

            cache.Store(SessionId, new SessionData(SessionStateActions.None, 30), TimeSpan.FromMinutes(30));

            var data = cache.GetByCacheKey(_primaryKey); 
            Assert.NotNull(data);

            var data2 = cache.GetByCacheKey(_backupKey);
            Assert.NotNull(data2);

            cache.Remove(SessionId);
        }

        [Test]
        public void RemoveSessionTest()
        {
            cache.ResetMemcachedClient("sessionManagement/memcached");

            cache.Store(SessionId, new SessionData(SessionStateActions.None, 30), TimeSpan.FromMinutes(30));

            var data = cache.GetByCacheKey(_primaryKey);
            var data2 = cache.GetByCacheKey(_backupKey);
            Assert.NotNull(data);
            Assert.NotNull(data2);

            cache.Remove(SessionId);

            data = cache.GetByCacheKey(_primaryKey);
            data2 = cache.GetByCacheKey(_backupKey);
            Assert.IsNull(data);
            Assert.IsNull(data2);
        }

        [Test]
        public void DefaultLocatorTest()
        {
            cache.ResetMemcachedClient("test/defaultLocator");

            cache.Store(SessionId, new SessionData(SessionStateActions.None, 30), TimeSpan.FromMinutes(30));

            var data = cache.GetByCacheKey(_primaryKey);
            var data2 = cache.GetByCacheKey(_backupKey);
            Assert.NotNull(data);
            Assert.IsNull(data2, "Don't backup sessions if not using backup node locator");

            cache.Remove(SessionId);
            
        }

        [Test]
        public void SingleNodeTest()
        {
            cache.ResetMemcachedClient("test/singleServer");

            cache.Store(SessionId, new SessionData(SessionStateActions.None, 30), TimeSpan.FromMinutes(30));

            var data = cache.GetByCacheKey(_primaryKey);
            var data2 = cache.GetByCacheKey(_backupKey);
            Assert.NotNull(data);
            Assert.IsNull(data2, "Don't backup sessions if using single node");

            cache.Remove(SessionId);

        }
    }
}
