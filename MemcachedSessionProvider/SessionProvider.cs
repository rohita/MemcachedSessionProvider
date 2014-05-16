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

#region MSDN References
/*
Implementing a Session-State Store Provider, http://msdn.microsoft.com/en-us/library/ms178587.aspx
Sample Session-State Store Provider, http://msdn.microsoft.com/en-us/library/ms178589.aspx
*/
#endregion

using System;
using System.Configuration;
using System.Web;
using System.Web.Configuration;
using System.Web.SessionState;

namespace MemcachedSessionProvider
{
    public class SessionProvider : SessionStateStoreProviderBase
    {
        private TimeSpan _sessionTimeout;
        private readonly SessionCacheWithBackup _sessionCache = SessionCacheWithBackup.Instance;
        
        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (String.IsNullOrEmpty(name))
                name = "MemcachedSessionProvider";

            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Memcached Session Provider");
            }

            // Initialize the abstract base class.
            base.Initialize(name, config);

            // Get <sessionState> Timeout value
            var objConfig = (SessionStateSection)WebConfigurationManager.GetSection("system.web/sessionState");
            _sessionTimeout = objConfig.Timeout;
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request and performs 
        /// any initialization required.
        /// </summary>
        public override void InitializeRequest(HttpContext context)
        {
            // nothing required for our memcached provider
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request and performs 
        /// any cleanup required.
        /// </summary>
        public override void EndRequest(HttpContext context)
        {
            // nothing required for our memcached provider
        }

        /// <summary>
        /// Frees any resources no longer in use by the session-state store provider.
        /// </summary>
        public override void Dispose()
        {
             
        }


        /// <summary>
        /// Takes as input the HttpContext instance for the current request and the 
        /// SessionID value for the current request. Retrieves session values and information 
        /// from Memcached. 
        /// 
        /// This method is supposed to lock the session-item data at the data store for the duration 
        /// of the request. But this implementation is "lockless". To keep the performance fast,
        /// there is no locking of any session data.
        /// 
        /// The GetItemExclusive method sets several output-parameter values that inform the calling 
        /// SessionStateModule about the state of the current session-state item in the data store.
        /// If no session item data is found at the data store, the GetItemExclusive method sets the 
        /// locked output parameter to false and returns null. This causes SessionStateModule to call 
        /// the CreateNewStoreData method to create a new SessionStateStoreData object for the request.
        /// 
        /// If session-item data is found at the data store, the GetItemExclusive method returns the item. 
        /// The locked output parameter is still set to false, the lockAge output parameter is set to 0, 
        /// the lockId output parameter is set to DateTime.UtcNow.Ticks. This implementation is not 
        /// keeping track of lockId.   
        /// 
        /// The actionFlags parameter is used with sessions whose Cookieless property is true, when 
        /// the regenerateExpiredSessionId attribute is set to true. An actionFlags value set to 1
        /// (SessionStateActions.InitializeItem) indicates that the entry in the session data store 
        /// is a new session that requires initialization. Uninitialized entries in the session data store 
        /// are created by a call to the CreateUninitializedItem method. If the item from the session data 
        /// store is already initialized, the actionFlags parameter is set to zero (SessionStateActions.None).
        /// 
        /// To support cookieless sessions, the actionFlags output parameter is set to the value returned 
        /// from the session data store for the current item. If the actionFlags parameter value for the 
        /// requested session-store item equals the InitializeItem enumeration value (1), the the value in 
        /// the data store is set to zero after setting the actionFlags out parameter.
        /// </summary>
        public override SessionStateStoreData GetItemExclusive(
            HttpContext context, string id,
            out bool locked, out TimeSpan lockAge, out object lockId,
            out SessionStateActions actions)
        {
            return GetSessionStoreItem(context, id, out locked, out lockAge, out lockId, out actions);
        }

        /// <summary>
        /// This method performs the same work as the GetItemExclusive method. 
        /// The GetItem method is called when the EnableSessionState attribute is set to ReadOnly.
        /// </summary>
        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, 
            out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return GetSessionStoreItem(context, id, out locked, out lockAge, out lockId, out actions);
        }

        private SessionStateStoreData GetSessionStoreItem(
            HttpContext context,
            string id,
            out bool locked,
            out TimeSpan lockAge,
            out object lockId,
            out SessionStateActions actions)
        {
            locked  = false;     // There is no locking of any session data
            lockAge = TimeSpan.Zero;
            lockId  = DateTime.UtcNow.Ticks;
            actions = SessionStateActions.None;

            var cacheItem = _sessionCache.Get(id);
            if (cacheItem == null)
            {
                return null;
            }

            SessionStateStoreData sessionData = cacheItem.Deserialize(context);
            actions = cacheItem.GetActionFlag();
            return sessionData;
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request, the SessionID 
        /// value for the current request, a SessionStateStoreData object that contains the 
        /// current session values to be stored, the lock identifier for the current request, 
        /// and a value that indicates whether the data to be stored is for a new session or 
        /// an existing session.
        /// 
        /// For Memcached, it doesn't matter if the newItem parameter is true. This method will
        /// update the data store with the supplied values.
        /// 
        /// Note that there is no lock on the data that is to be released. Also, since the lockid
        /// is not tracked, there is check to see if session data matches the supplied lock identifier
        /// before values are updated. Whichever request calls this method last, will update the 
        /// session data. 
        /// 
        /// After the SetAndReleaseItemExclusive method is called, the ResetItemTimeout method is 
        /// called by SessionStateModule to update the expiration date and time of the session-item data.
        /// </summary>
        public override void SetAndReleaseItemExclusive(
            HttpContext context,
            string id,
            SessionStateStoreData item,
            object lockId,
            bool newItem)
        {
            var cacheItem = new SessionData(SessionStateActions.None, item.Timeout);
            cacheItem.Serialize((SessionStateItemCollection)item.Items);
            _sessionCache.Store(id, cacheItem, new TimeSpan(0, item.Timeout, 0));
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request, the SessionID value 
        /// for the current request, and the lock identifier for the current request, and releases 
        /// the lock on an item in the session data store. This method is called when the GetItem 
        /// or GetItemExclusive method is called and the data store specifies that the requested 
        /// item is locked, but the lock age has exceeded the ExecutionTimeout value. The lock is 
        /// cleared by this method, freeing the item for use by other requests.
        /// </summary>
        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            //no lock to release
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request, the SessionID value 
        /// for the current request, and the lock identifier for the current request, and deletes 
        /// the session information from the data store where the data store item matches the supplied 
        /// SessionID value. This method is called when the Abandon method is called.
        /// </summary>
        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            _sessionCache.Remove(id);
        }

        /// <summary>
        /// Takes as input the HttpContext instance for the current request, and the SessionID value 
        /// for the current request, and adds an uninitialized item to the session data store with 
        /// an actionFlags value of InitializeItem.
        /// 
        /// The CreateUninitializedItem method is used with cookieless sessions when the 
        /// regenerateExpiredSessionId attribute is set to true, which causes SessionStateModule to 
        /// generate a new SessionID value when an expired session ID is encountered.
        /// 
        /// The process of generating a new SessionID value requires the browser to be redirected 
        /// to a URL that contains the newly generated session ID. The CreateUninitializedItem method 
        /// is called during an initial request that contains an expired session ID. After 
        /// SessionStateModule acquires a new SessionID value to replace the expired session ID, it 
        /// calls the CreateUninitializedItem method to add an uninitialized entry to the session-state 
        /// data store. The browser is then redirected to the URL containing the newly generated SessionID 
        /// value. The existence of the uninitialized entry in the session data store ensures that the 
        /// redirected request with the newly generated SessionID value is not mistaken for a request 
        /// for an expired session, and instead is treated as a new session.
        /// 
        /// The uninitialized entry in the session data store is associated with the newly generated 
        /// SessionID value and contains only default values, including an expiration date and time, 
        /// and a value that corresponds to the actionFlags parameter of the GetItem and GetItemExclusive 
        /// methods. The uninitialized entry in the session state store should include an actionFlags 
        /// value equal to the InitializeItem enumeration value (1). This value is passed to 
        /// SessionStateModule by the GetItem and GetItemExclusive methods and specifies for 
        /// SessionStateModule that the current session is a new session. SessionStateModule will then 
        /// initialize the new session and raise the Session_OnStart event.
        /// </summary>
        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            var cacheItem = new SessionData(SessionStateActions.InitializeItem, timeout);
            _sessionCache.Store(id, cacheItem, new TimeSpan(0, timeout, 0));
        }
        
        /// <summary>
        /// Takes as input the HttpContext instance for the current request and the Timeout value for 
        /// the current session, and returns a new SessionStateStoreData object with an empty 
        /// ISessionStateItemCollection object, an HttpStaticObjectsCollection collection, and the 
        /// specified Timeout value. The HttpStaticObjectsCollection instance for the ASP.NET 
        /// application can be retrieved using the GetSessionStaticObjects method.
        /// </summary>
        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(
                new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context),
                timeout);
        }

        /// <summary>
        /// Takes as input a delegate that references the Session_OnEnd event defined in the 
        /// Global.asax file. If the session-state store provider supports the Session_OnEnd event, 
        /// a local reference to the SessionStateItemExpireCallback parameter is set and the method 
        /// returns true; otherwise, the method returns false.
        /// </summary>
        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            // not supported.
            return false;
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var obj = _sessionCache.Get(id);

            if (obj != null)
            {
                _sessionCache.Store(id, obj, _sessionTimeout);
            }
        }
    }
}
