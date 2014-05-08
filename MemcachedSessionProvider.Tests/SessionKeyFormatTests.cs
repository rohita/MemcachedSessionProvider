using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using NUnit.Framework;

namespace MemcachedSessionProvider.Tests
{
    [TestFixture]
    public class SessionKeyFormatTests
    {
        [TestCase("bak:__AspSession_testapp_abcd456", "bak:__AspSession_testapp_abcd456")]
        [TestCase("bak:__AspSession_testapp_35c30248-2cdd-4a34-ac18-4749aeeb350b", "35c30248-2cdd-4a34-ac18-4749aeeb350b")]
        [TestCase("bak:__AspSession_testapp_abcd456", "__AspSession_testapp_abcd456")]
        [TestCase("bak:__AspSession_testapp_", "")]
        [TestCase("bak:__AspSession_testapp_", "bak:__AspSession_testapp_")]
        public void TestGetBackupKey(string expected, string sessionId)
        {
            var s = new SessionKeyFormat("testapp");
            Assert.AreEqual(expected, s.GetBackupKey(sessionId));
        }

        [TestCase("bak:__AspSession__35c30248-2cdd-4a34-ac18-4749aeeb350b", "35c30248-2cdd-4a34-ac18-4749aeeb350b")]
        [TestCase("bak:__AspSession__abcd456", "__AspSession__abcd456")]
        [TestCase("bak:__AspSession__", "")]
        public void TestGetBackupKey2(string expected, string sessionId)
        {
            var s = new SessionKeyFormat(null);
            Assert.AreEqual(expected, s.GetBackupKey(sessionId));
        }

        [TestCase("__AspSession_testapp_abcd456", "bak:__AspSession_testapp_abcd456")]
        [TestCase("__AspSession_testapp_35c30248-2cdd-4a34-ac18-4749aeeb350b", "35c30248-2cdd-4a34-ac18-4749aeeb350b")]
        [TestCase("__AspSession_testapp_abcd456", "__AspSession_testapp_abcd456")]
        [TestCase("__AspSession_testapp_", "")]
        [TestCase("__AspSession_testapp_", "bak:__AspSession_testapp_")]
        public void TestGetPrimaryKey(string expected, string sessionId)
        {
            var s = new SessionKeyFormat("testapp");
            Assert.AreEqual(expected, s.GetPrimaryKey(sessionId));
        }

        [TestCase("__AspSession_testapp_abcd456", "bak:__AspSession_testapp_abcd456")]
        [TestCase("__AspSession__35c30248-2cdd-4a34-ac18-4749aeeb350b", "35c30248-2cdd-4a34-ac18-4749aeeb350b")]
        [TestCase("__AspSession_testapp_abcd456", "__AspSession_testapp_abcd456")]
        [TestCase("__AspSession__", "")]
        [TestCase("__AspSession__abcd456", "__AspSession__abcd456")]
        public void TestGetPrimaryKey2(string expected, string sessionId)
        {
            var s = new SessionKeyFormat("");
            Assert.AreEqual(expected, s.GetPrimaryKey(sessionId));
        }

        [TestCase(false, "abcd456")]
        [TestCase(false, "")]
        [TestCase(true, "bak:__AspSession_testapp_abcd456")]
        [TestCase(true, "bak:__AspSession__abcd456")]
        [TestCase(true, "bak:__AspSession__")]
        [TestCase(false, "__AspSession_testapp_abcd456")]
        [TestCase(false, "__AspSession__abcd456")]
        [TestCase(false, "__AspSession__")]
        public void TestIsBackupKey(bool expected, string sessionId)
        {
            var s = new SessionKeyFormat(null);
            Assert.AreEqual(expected, s.IsBackupKey(sessionId));
        }

        [TestCase(false, "abcd456")]
        [TestCase(false, "")]
        [TestCase(false, "bak:__AspSession_testapp_abcd456")]
        [TestCase(false, "bak:__AspSession__abcd456")]
        [TestCase(false, "bak:__AspSession__")]
        [TestCase(true, "__AspSession_testapp_abcd456")]
        [TestCase(true, "__AspSession__abcd456")]
        [TestCase(true, "__AspSession__")]
        public void TestIsPrimaryKey(bool expected, string sessionId)
        {
            var s = new SessionKeyFormat("testapp");
            Assert.AreEqual(expected, s.IsPrimaryKey(sessionId));
        }

        [TestCase(true, "__AspSession_testapp_abcd456")]
        [TestCase(true, "__AspSession__abcd456")]
        [TestCase(true, "__AspSession__")]
        public void TestIsPrimaryKey2(bool expected, string sessionId)
        {
            var s = new SessionKeyFormat(null);
            Assert.AreEqual(expected, s.IsPrimaryKey(sessionId));
        }
    }
}
