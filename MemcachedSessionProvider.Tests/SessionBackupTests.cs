using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.SessionState;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace MemcachedSessionProvider.Tests
{
    [TestFixture]
    public class SessionBackupTests
    {
        private const string N2Key = "abcdefgh_34567890";
        private const string N1Key = "abcdefgh";
        private const string N2Key2Node = "345678906789";
        ISocketPoolConfiguration _s = new SocketPoolConfiguration();
        private SessionCacheWithBackup cache = SessionCacheWithBackup.Instance;

        [Test]
        public void TestSingleClientPrimaryDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);

            var newClient = new MockMemcachedClient(new List<IMemcachedNode> { n1, n2, n3, n4 });
            cache.ResetMemcachedClient(newClient, null);

            cache.Store(N1Key, new SessionData(SessionStateActions.None, 23), TimeSpan.FromMinutes(30));

            newClient.SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            var data = cache.Get(N1Key);
            Assert.AreEqual(23, data.Timeout);
        }

        [Test]
        public void TestSingleClientPrimaryUp()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);
            var nodes = new List<IMemcachedNode> {n1, n2, n3, n4};

            var newClient = new MockMemcachedClient(nodes);
            cache.ResetMemcachedClient(newClient, null);

            cache.Store(N1Key, new SessionData(SessionStateActions.None, 23), TimeSpan.FromMinutes(30));

            newClient.SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            var data = cache.Get(N1Key);
            Assert.AreEqual(23, data.Timeout);

            newClient.SetNodeAlive(n1, new List<IMemcachedNode> { n1, n2, n3, n4 });

            data = cache.Get(N1Key);
            Assert.AreEqual(23, data.Timeout);
        }

        [Test]
        public void TestSingleClientPrimaryDown2NdStore()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);

            var newClient = new MockMemcachedClient(new List<IMemcachedNode> { n1, n2, n3, n4 });
            cache.ResetMemcachedClient(newClient, null);

            cache.Store(N1Key, new SessionData(SessionStateActions.None, 23), TimeSpan.FromMinutes(30));

            newClient.SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            cache.Store(N1Key, new SessionData(SessionStateActions.None, 25), TimeSpan.FromMinutes(30));

            var data = cache.Get(N1Key);
            Assert.AreEqual(25, data.Timeout);
        }


        [Test]
        public void TestTwoClientsPrimaryDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);

            var s1 = new SessionNodeLocatorImpl();
            s1.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            var newClient = new MockMemcachedClient(new List<IMemcachedNode> { n1, n2, n3, n4 });
            cache.ResetMemcachedClient(newClient, s1);

            cache.Store(N1Key, new SessionData(SessionStateActions.None, 23), TimeSpan.FromMinutes(30));

            newClient.SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            cache.Store(N1Key, new SessionData(SessionStateActions.None, 25), TimeSpan.FromMinutes(30));

            var s2 = new SessionNodeLocatorImpl();
            s2.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            cache.ResetMemcachedClient(newClient, s2);
            newClient.SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            var data = cache.Get(N1Key);
            Assert.AreEqual(25, data.Timeout);
        }

        private IMemcachedNode GetNode(int num)
        {
            var p1 = new IPEndPoint(IPAddress.Parse(string.Format("10.10.10.{0}", num)), 11211);
            return new MemcachedNode(p1, _s);
        }
    }
}
