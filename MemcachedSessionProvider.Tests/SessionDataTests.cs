using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.SessionState;
using NUnit.Framework;

namespace MemcachedSessionProvider.Tests
{
    [TestFixture]
    public class SessionDataTests
    {
        [Test]
        public void TestDeserializeHandlesNull()
        {
            var request = new SimpleWorkerRequest("", "", "", null, new StringWriter());
            var context = new HttpContext(request);
            var s = new SessionData(SessionStateActions.None, 10); 
            Assert.DoesNotThrow(() => s.Deserialize(context));
        }
    }
}
