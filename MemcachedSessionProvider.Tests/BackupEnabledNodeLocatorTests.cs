using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace MemcachedSessionProvider.Tests
{
    [TestFixture]
    public class BackupEnabledNodeLocatorTests
    {
        private const string N1Key = "__AspSession_abcdefgh_34567890";
        private const string N2Key = "__AspSession_abcdefgh";
        private const string BackupPrefix = "bak:"; 

        ISocketPoolConfiguration s = new SocketPoolConfiguration();
        private IMemcachedNodeLocator locator; 
        
        [SetUp]
        public void Setup()
        {
            locator = new BackupEnabledNodeLocator(); 
        }

        [Test]
        public void TestSingleNode()
        {
            var n1 = GetNode(1);
            locator.Initialize(new List<IMemcachedNode> {n1});

            var primary = locator.Locate(N1Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.IsNull(backup, "No back if single node");
        }

        [Test]
        public void TestSingleNodeDown()
        {
            var n1 = GetNode(1);
            locator.Initialize(new List<IMemcachedNode> { n1 });
            SetNodeDead(n1);

            var primary = locator.Locate(N1Key);
            Assert.IsNull(primary);

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.IsNull(backup);
        }

        [Test]
        public void TestTwoNodes()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });

            var primary = locator.Locate(N1Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint, "Backup is on next node");
        }

        [Test]
        public void TestTwoNodesLoopBack()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });

            var primary = locator.Locate(N2Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.AreEqual(n1.EndPoint, backup.EndPoint, "Backup loops back to first if primary is on last node");
        }

        [Test]
        public void TestTwoNodesWithFirstNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SetNodeDead(n1);

            var primary = locator.Locate(N1Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.IsNull(backup, "No backup if single node");
        }

        [Test]
        public void TestTwoNodesWithSecondNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SetNodeDead(n2);

            var primary = locator.Locate(N2Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.IsNull(backup, "No backup if single node");
        }

        [Test]
        public void TestTwoNodesWithAllNodesDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SetNodeDead(n1);
            SetNodeDead(n2);

            var primary = locator.Locate(N2Key);
            Assert.IsNull(primary);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.IsNull(backup);
        }

        [Test]
        public void TestThreeNodes()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3 });

            var primary = locator.Locate(N2Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.AreEqual(n3.EndPoint, backup.EndPoint);
        }

        [Test]
        public void TestThreeNodesWith3RdNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3 });
            SetNodeDead(n3);

            var primary = locator.Locate(N2Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.AreEqual(n1.EndPoint, backup.EndPoint, "Backup skips dead nodes");
        }

        [Test]
        public void TestThreeNodesWith2NdNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3 });
            SetNodeDead(n2);

            var primary = locator.Locate(N1Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n3.EndPoint, backup.EndPoint, "Backup skips dead nodes");
        }

        private IMemcachedNode GetNode(int num)
        {
            var p1 = new IPEndPoint(IPAddress.Parse(string.Format("10.10.10.{0}", num)), 80);
            return new MemcachedNode(p1, s);
        }

        private void SetNodeDead(IMemcachedNode n1)
        {
            // Having to use reflection to set a private field
            var prop = n1.GetType().GetField("internalPoolImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            var internalPoolImpl = prop.GetValue(n1);
            var prop2 = internalPoolImpl.GetType().GetField("isAlive", BindingFlags.NonPublic | BindingFlags.Instance);
            prop2.SetValue(internalPoolImpl, false);
        }
        
    }
}
