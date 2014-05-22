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
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Threading;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

namespace MemcachedSessionProvider
{
    internal sealed class SessionCacheWithBackup
    {
        private static readonly SessionCacheWithBackup _instance = new SessionCacheWithBackup();
        private volatile IMemcachedClient _client;
        private SessionKeyFormat _sessionKeyFormat;
        private const string DefaultConfigSection = "sessionManagement/memcached";
        private MemcachedClientSection _memcachedClientSection;
        private SessionNodeLocatorImpl _locatorImpl;
        private object _clientAccessLock = new object();

        private SessionCacheWithBackup()
        {
            _memcachedClientSection = ConfigurationManager.GetSection(DefaultConfigSection) as MemcachedClientSection;
            _locatorImpl = new SessionNodeLocatorImpl();
            _sessionKeyFormat = new SessionKeyFormat();
        }

        public static SessionCacheWithBackup Instance 
        {
            get { return _instance; }            
        }

        public void InitializeClient()
        {
            if (_client != null) return;

            lock (_clientAccessLock)
            {
                if (_client == null)
                    _client = new MemcachedClient(_memcachedClientSection);
            }
        }

        public SessionData Get(string sessionId)
        {
            var cacheKey = _sessionKeyFormat.GetPrimaryKey(sessionId);
            var data = _client.Get<SessionData>(cacheKey);

            if (data != null)
            {
                // Check if a new primary was assigned
                data = CheckForNewer(sessionId, data);
            }
            else if (IsBackupEnabled()) // try backup
            {
                var backupKey = _sessionKeyFormat.GetBackupKey(sessionId);
                data = _client.Get<SessionData>(backupKey);

                if (data != null)  
                {
                    // Data found on backup node. This means primary is down.
                    // Check on new primary
                    data = CheckForNewer(sessionId, data);
                }
            }

            return data; 
        }

        private SessionData CheckForNewer(string sessionId, SessionData data)
        {
            string cacheKey = _sessionKeyFormat.GetPrimaryKey(sessionId);

            // try if there is fresher copy
            // This happens when a node goes up or down
            if (AssignPrimaryBackupNodes(cacheKey))
            {
                var newData = _client.Get<SessionData>(cacheKey);
                if (newData == null || data.SavedAt > newData.SavedAt)
                {
                    // If not found or older, that means this client hit the key first
                    // so relocate session for next call
                    Store(sessionId, data, TimeSpan.FromMinutes(data.Timeout));
                }
                else
                {
                    // else found newer, that means some other client already updated this
                    data = newData;
                }
            }

            return data;
        }

        public void Store(string sessionId, SessionData cacheItem, TimeSpan timeout)
        {
            var cacheKey = _sessionKeyFormat.GetPrimaryKey(sessionId);
            AssignPrimaryBackupNodes(cacheKey);

            cacheItem.SavedAt = DateTime.UtcNow.Ticks;
            _client.Store(StoreMode.Set, cacheKey, cacheItem, timeout);

            if (IsBackupEnabled()) // backup
            {
                var backupKey = _sessionKeyFormat.GetBackupKey(sessionId);
                _client.Store(StoreMode.Set, backupKey, cacheItem, timeout);
            }
        }

        public void Remove(string sessionId)
        {
            var cacheKey = _sessionKeyFormat.GetPrimaryKey(sessionId);
            _client.Remove(cacheKey);

            if (IsBackupEnabled())
            {
                _client.Remove(_sessionKeyFormat.GetBackupKey(sessionId)); 
            }
        }

        public void InitializeLocator(IList<IMemcachedNode> nodes)
        {
            _locatorImpl.Initialize(nodes);
        }

        public IMemcachedNode Locate(string key)
        {
            return _locatorImpl.Locate(key);
        }

        public IEnumerable<IMemcachedNode> GetWorkingNodes()
        {
            return _locatorImpl.GetWorkingNodes();
        } 

        private bool IsBackupEnabled()
        {
            return _memcachedClientSection.Servers.Count > 1
                && IsUsingSessionNodeLocator(); 
        }

        private bool IsUsingSessionNodeLocator()
        {
            return _memcachedClientSection.NodeLocator.Type == typeof (SessionNodeLocator);
        }

        public bool AssignPrimaryBackupNodes(string cacheKey)
        {
            if (IsUsingSessionNodeLocator())
            {
                return _locatorImpl.AssignPrimaryBackupNodes(cacheKey);
            }

            return false; 
        }


        internal void ResetMemcachedClient(string memcachedConfigSection)
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null; 
            }
            _locatorImpl = new SessionNodeLocatorImpl();
            _memcachedClientSection = ConfigurationManager.GetSection(memcachedConfigSection) as MemcachedClientSection;
            InitializeClient();
        }

        internal void ResetMemcachedClient(IMemcachedClient newClient, SessionNodeLocatorImpl newLocator)
        {
            if (_client != null) _client.Dispose();
            _client = newClient;
            _locatorImpl = newLocator ?? _locatorImpl; 
        }

        internal void ResetLocator()
        {
            _locatorImpl = new SessionNodeLocatorImpl();
        }

        internal SessionData GetByCacheKey(string key)
        {
            return _client.Get<SessionData>(key);
        }
        
    }
}
