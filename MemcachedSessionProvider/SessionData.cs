#region [License]
/* ************************************************************
 * 
 *    Copyright (c) 2014 Rohit Agarwal
 *    
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *    
 *        http://www.apache.org/licenses/LICENSE-2.0
 *    
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *    
 * ************************************************************/
#endregion

using System;
using System.IO;
using System.Web;
using System.Web.SessionState;

namespace MemcachedSessionProvider
{
    [Serializable]
    internal class SessionData
    {
        private readonly int _actionFlag;
        private readonly int _timeout; 
        private byte[] _serializedSessionData;

   
        public SessionData(SessionStateActions actionFlag, int timeout)
        {
            _actionFlag = (int) actionFlag;
            _timeout = timeout; 
            _serializedSessionData = null; 
        }

        public void Serialize(SessionStateItemCollection items)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);

            if (items != null)
                items.Serialize(writer);

            writer.Close();

            _serializedSessionData = ms.ToArray();
        }

        public SessionStateStoreData Deserialize(HttpContext context)
        {
            var ms = _serializedSessionData == null 
                ? new MemoryStream() 
                : new MemoryStream(_serializedSessionData);

            var sessionItems = new SessionStateItemCollection();

            if (ms.Length > 0)
            {
                var reader = new BinaryReader(ms);
                sessionItems = SessionStateItemCollection.Deserialize(reader);
            }

            return new SessionStateStoreData(sessionItems,
              SessionStateUtility.GetSessionStaticObjects(context),
              _timeout);
        }

        public SessionStateActions GetActionFlag()
        {
            return (SessionStateActions) _actionFlag; 
        }

    }
}
