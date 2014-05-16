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
using System.Configuration;
using System.Reflection;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

namespace MemcachedSessionProvider
{
    internal class SessionCacheWithBackup
    {
        private static readonly SessionCacheWithBackup _instance = new SessionCacheWithBackup();
        private MemcachedClient _client;
        private SessionKeyFormat _sessionKeyFormat;
        private const string DefaultConfigSection = "enyim.com/memcached";
        private MemcachedClientSection _memcachedClientSection;

        private SessionCacheWithBackup()
        {
            _memcachedClientSection = ConfigurationManager.GetSection(DefaultConfigSection) as MemcachedClientSection;
            _client = new MemcachedClient(_memcachedClientSection);
            _sessionKeyFormat = new SessionKeyFormat();
        }

        public static SessionCacheWithBackup Instance 
        {
            get { return _instance; }            
        }

        public SessionData Get(string sessionId)
        {
            var cacheKey = _sessionKeyFormat.GetPrimaryKey(sessionId);
            var data = _client.Get<SessionData>(cacheKey);
            if (data == null && IsBackupEnabled()) // try backup
            {
                var backupKey = _sessionKeyFormat.GetBackupKey(sessionId);
                data = _client.Get<SessionData>(backupKey);

                if (data != null)
                {
                    // relocate session
                    Store(sessionId, data, TimeSpan.FromMinutes(data.Timeout));
                }
            }

            return data; 
        }

        public void Store(string sessionId, SessionData cacheItem, TimeSpan timeout)
        {
            var cacheKey = _sessionKeyFormat.GetPrimaryKey(sessionId);
            SessionNodeLocatorImpl.Instance.AssignPrimaryBackupNodes(cacheKey);
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

        private bool IsBackupEnabled()
        {
            return _memcachedClientSection.Servers.Count > 1 
                && _memcachedClientSection.NodeLocator.Type == typeof (SessionNodeLocator); 
        }

        internal void ResetMemcachedClient(string memcachedConfigSection)
        {
            _client.Dispose();
            _memcachedClientSection = ConfigurationManager.GetSection(memcachedConfigSection) as MemcachedClientSection;
            _client = new MemcachedClient(_memcachedClientSection);
        }

        internal SessionData GetByCacheKey(string key)
        {
            return _client.Get<SessionData>(key);
        }
        
    }
}
