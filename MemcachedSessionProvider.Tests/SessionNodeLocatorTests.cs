using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using NUnit.Framework;

namespace MemcachedSessionProvider.Tests
{
    [TestFixture]
    public class SessionNodeLocatorTests
    {
        private const string N2Key = "__AspSession_abcdefgh_34567890";
        private const string N1Key = "__AspSession_abcdefgh";
        private const string N2Key2Node = "__AspSession_345678906789";
        private const string BackupPrefix = "bak:"; 

        ISocketPoolConfiguration s = new SocketPoolConfiguration();
        private IMemcachedNodeLocator locator;
            
        [SetUp]
        public void Setup()
        {
            locator = new SessionNodeLocator(); 

        }

        [Test]
        public void TestSingleNode()
        {
            var n1 = GetNode(1);
            locator.Initialize(new List<IMemcachedNode> {n1});

            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

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
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

            SetNodeDead(n1, new List<IMemcachedNode>());

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
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

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
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N2Key2Node);

            var primary = locator.Locate(N2Key2Node);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key2Node);
            Assert.AreEqual(n1.EndPoint, backup.EndPoint, "Backup loops back to first if primary is on last node");
        }

        [Test]
        public void TestTwoNodesWithFirstNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

            SetNodeDead(n1, new List<IMemcachedNode>{n2});

            var primary = locator.Locate(N1Key);
            Assert.IsNull(primary, "Primary Node is down");

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint, "Backup is available");
        }

        [Test]
        public void TestBackupTwoNodesWithFirstNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);
            
            SetNodeDead(n1, new List<IMemcachedNode> { n2 });

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint);

            // now primary moves
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

            var primary = locator.Locate(N1Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            // no backup
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.IsNull(backup, "No backup if single node");
        }
        
        [Test]
        public void TestTwoNodesWithBackupNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N2Key);

            SetNodeDead(n2, new List<IMemcachedNode>{n1});

            var primary = locator.Locate(N2Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.IsNull(backup, "Don't return dead node");
        }

        [Test]
        public void TestTwoNodesWithAllNodesDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            locator.Initialize(new List<IMemcachedNode> { n1, n2 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N2Key);

            SetNodeDead(n1, new List<IMemcachedNode>{n2});
            SetNodeDead(n2, new List<IMemcachedNode>());

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
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N2Key);

            var primary = locator.Locate(N2Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);

            var backup = locator.Locate(BackupPrefix + N2Key);
            Assert.AreEqual(n3.EndPoint, backup.EndPoint);
        }

        [Test]
        public void TestThreeNodesWithBackupNodeDown()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N2Key2Node);

            SetNodeDead(n3, new List<IMemcachedNode>{n1, n2});

            var backup = locator.Locate(BackupPrefix + N2Key2Node);
            Assert.IsNull(backup);

            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N2Key2Node);

            backup = locator.Locate(BackupPrefix + N2Key2Node);
            Assert.AreEqual(n1.EndPoint, backup.EndPoint, "Backup skips dead nodes");
        }

        [Test]
        public void TestBackupNodeDoesntMoveAfterNodeFailure()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4); 
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint, "Backup on next node");

            SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });
            
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint, "After primary node failure, don't move the backup node");
        }

        [Test]
        public void TestBackupNodeMoveAfterNodeFailure()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);
            
            SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            var primary = locator.Locate(N1Key);
            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.IsNull(primary);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint, "After primary node failure, don't move the backup node");

            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);
            primary = locator.Locate(N1Key);
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);
            Assert.AreEqual(n3.EndPoint, backup.EndPoint, "Backup moves after a call to Assign");
        }

        [Test]
        public void TestNodeRecovery()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);

            SetNodeDead(n1, new List<IMemcachedNode> { n2, n3, n4 });

            var primary = locator.Locate(N1Key);
            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.IsNull(primary);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint, "After primary node failure, don't move the backup node");

            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);
            primary = locator.Locate(N1Key);
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);
            Assert.AreEqual(n3.EndPoint, backup.EndPoint, "Backup moves after a call to Assign");

            SetNodeAlive(n1, new List<IMemcachedNode> { n1, n2, n3, n4 });

            // no move yet
            primary = locator.Locate(N1Key);
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n2.EndPoint, primary.EndPoint);
            Assert.AreEqual(n3.EndPoint, backup.EndPoint);

            // Reassign
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key);
            primary = locator.Locate(N1Key);
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint);
            
        }

        [Test]
        public void TestNodeWithAppRestart()
        {
            var n1 = GetNode(1);
            var n2 = GetNode(2);
            var n3 = GetNode(3);
            var n4 = GetNode(4);
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(N1Key); //save N1Key

            var primary = locator.Locate(N1Key);
            var backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint);

            SessionNodeLocatorImpl.Instance.ResetAllKeys(); // client app went down
            locator.Initialize(new List<IMemcachedNode> { n1, n2, n3, n4 });
            primary = locator.Locate(N1Key);
            backup = locator.Locate(BackupPrefix + N1Key);
            Assert.AreEqual(n1.EndPoint, primary.EndPoint);
            Assert.AreEqual(n2.EndPoint, backup.EndPoint);
        }

        [Test]
        public void Test2()
        {
            Assert.IsNull(default(SessionData));
            Console.WriteLine(default(SessionData));
        }



        private IMemcachedNode GetNode(int num)
        {
            var p1 = new IPEndPoint(IPAddress.Parse(string.Format("10.10.10.{0}", num)), 11211);
            return new MemcachedNode(p1, s);
        }

        private void SetNodeDead(IMemcachedNode n1, List<IMemcachedNode> activeNodes)
        {
            // Having to use reflection to set a private field
            var prop = n1.GetType().GetField("internalPoolImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            var internalPoolImpl = prop.GetValue(n1);
            var prop2 = internalPoolImpl.GetType().GetField("isAlive", BindingFlags.NonPublic | BindingFlags.Instance);
            prop2.SetValue(internalPoolImpl, false);

            locator = new SessionNodeLocator();
            locator.Initialize(activeNodes);
        }

        private void SetNodeAlive(IMemcachedNode n1, List<IMemcachedNode> activeNodes)
        {
            // Having to use reflection to set a private field
            var prop = n1.GetType().GetField("internalPoolImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            var internalPoolImpl = prop.GetValue(n1);
            var prop2 = internalPoolImpl.GetType().GetField("isAlive", BindingFlags.NonPublic | BindingFlags.Instance);
            prop2.SetValue(internalPoolImpl, true);

            locator.Initialize(activeNodes);
        }
    }
}
