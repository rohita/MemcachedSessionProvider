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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Enyim.Caching;
using Enyim.Caching.Memcached;

namespace MemcachedSessionProvider
{
    internal class SessionNodeLocatorImpl
    {
        private static readonly SessionNodeLocatorImpl _instance = new SessionNodeLocatorImpl();

        private const int ServerAddressMutations = 100;

        // holds all server keys for mapping an item key to the server consistently
        private uint[] _keys;
        private Dictionary<uint, IMemcachedNode> _masterKeys;

        // used to lookup a server based on its key
        private Dictionary<uint, IMemcachedNode> _keyToServer;
        private Dictionary<uint, IMemcachedNode> _keyToBackup;
        private List<IMemcachedNode> _allServers;
        private ReaderWriterLockSlim _serverAccessLock;
        private SessionKeyFormat _sessionKeyFormat;

        private SessionNodeLocatorImpl()
        {
            _masterKeys = new Dictionary<uint, IMemcachedNode>(new UIntEqualityComparer());
            _keyToServer = new Dictionary<uint, IMemcachedNode>(new UIntEqualityComparer());
            _keyToBackup = new Dictionary<uint, IMemcachedNode>(new UIntEqualityComparer());
            _allServers = new List<IMemcachedNode>();
            _serverAccessLock = new ReaderWriterLockSlim();
            _sessionKeyFormat = new SessionKeyFormat();
        }

        public static SessionNodeLocatorImpl Instance
        {
            get { return _instance; }
        }

        public void Initialize(IList<IMemcachedNode> nodes)
        {
            _serverAccessLock.EnterWriteLock();

            try
            {
                _allServers = nodes.ToList();

                if (_keys == null)
                {
                    BuildIndex();
                    BuildMasterPrimaryAndBackup();
                }
                else
                {
                    ReBuildMaster();
                }

                
            }
            finally
            {
               _serverAccessLock.ExitWriteLock();
            }
        }

        public IMemcachedNode Locate(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            _serverAccessLock.EnterReadLock();

            try { return PerformLocate(key); }
            finally { _serverAccessLock.ExitReadLock(); }
        }

        public IEnumerable<IMemcachedNode> GetWorkingNodes()
        {
            return _allServers;
        }

        public void AssignPrimaryBackupNodes(string key)
        {
            _serverAccessLock.EnterReadLock();

            try { PerformNodeAssignment(key); }
            finally { _serverAccessLock.ExitReadLock(); }
        }

        internal void ResetAllKeys()
        {
            _keys = null;
            _masterKeys.Clear();
            _keyToServer.Clear();
            _keyToBackup.Clear();
            _allServers.Clear();
        }

        private IMemcachedNode PerformLocate(string key)
        {
            IMemcachedNode node;
            uint? itemKeyHash = GetPrimaryKeyHash(key);
            if (itemKeyHash == null || _allServers.Count == 0)
            {
                node = null;
            }
            else if (_sessionKeyFormat.IsBackupKey(key))
            {
                node = _keyToBackup.ContainsKey(itemKeyHash.Value) ? _keyToBackup[itemKeyHash.Value] : null;
            }
            else // Primary key
            {
                node = _keyToServer.ContainsKey(itemKeyHash.Value) ? _keyToServer[itemKeyHash.Value] : null;
            }

            if (node == null || node.IsAlive)
            {
                return node;
            }

            return null; 
        }

        private void PerformNodeAssignment(string key)
        {
            uint? itemKeyHash = GetPrimaryKeyHash(key);
            if (itemKeyHash == null || _allServers.Count == 0)
            {
                return;
            }

            var node = _masterKeys.ContainsKey(itemKeyHash.Value) ? _masterKeys[itemKeyHash.Value] : null;
            _keyToServer[itemKeyHash.Value] = node;
            var backupNode = FindNextNodeForBackup(node);
            _keyToBackup[itemKeyHash.Value] = backupNode;
        }

        private uint? GetPrimaryKeyHash(string rawKey)
        {
            if (_keys.Length == 0) return null;

            // clean the key from any backup prefix
            string key = _sessionKeyFormat.GetPrimaryKey(rawKey);

            uint itemKeyHash = BitConverter.ToUInt32(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(key)), 0);
            // get the index of the server assigned to this hash
            int foundIndex = Array.BinarySearch(_keys, itemKeyHash);

            // no exact match
            if (foundIndex < 0)
            {
                // this is the nearest server in the list
                foundIndex = ~foundIndex;

                if (foundIndex == 0)
                {
                    // it's smaller than everything, so use the last server (with the highest key)
                    foundIndex = _keys.Length - 1;
                }
                else if (foundIndex >= _keys.Length)
                {
                    // the key was larger than all server keys, so return the first server
                    foundIndex = 0;
                }
            }

            if (foundIndex < 0 || foundIndex > _keys.Length)
                return null;

            return _keys[foundIndex];
        }

        /// <summary>
        /// Get the next available node for the given one. For the last node
        /// the first one is returned. If this list contains only a 
        /// single node, conceptionally there's no next node, so null 
        /// is returned.
        /// </summary>
        private IMemcachedNode FindNextNodeForBackup(IMemcachedNode primaryNode)
        {
            if (primaryNode == null || _allServers.Count == 1)
            {
                return null;
            }

            var idx = _allServers.FindIndex(v => v.EndPoint.Equals(primaryNode.EndPoint));
            var nextIdx = (idx == _allServers.Count - 1) ? 0 : idx + 1;
            var backupNode = _allServers[nextIdx];
            if (backupNode.EndPoint.Equals(primaryNode.EndPoint))
            {
                backupNode = null;
            }

            return backupNode;
        }

        private void BuildIndex()
        {
            var keys = new uint[_allServers.Count * ServerAddressMutations];

            int nodeIdx = 0;

            foreach (IMemcachedNode node in _allServers)
            {
                var tmpKeys = GenerateKeys(node, ServerAddressMutations);
                tmpKeys.CopyTo(keys, nodeIdx);
                nodeIdx += ServerAddressMutations;
            }

            Array.Sort(keys);
            Interlocked.Exchange(ref _keys, keys);
        }

        private static uint[] GenerateKeys(IMemcachedNode node, int numberOfKeys)
        {
            const int keyLength = 4;
            const int partCount = 1; // (ModifiedFNV.HashSize / 8) / KeyLength; // HashSize is in bits, uint is 4 byte long

            var k = new uint[partCount * numberOfKeys];

            // every server is registered numberOfKeys times
            // using UInt32s generated from the different parts of the hash
            // i.e. hash is 64 bit:
            // 00 00 aa bb 00 00 cc dd
            // server will be stored with keys 0x0000aabb & 0x0000ccdd
            // (or a bit differently based on the little/big indianness of the host)
            string address = node.EndPoint.ToString();
            var md5 = MD5.Create();

            for (int i = 0; i < numberOfKeys; i++)
            {
                byte[] data = md5.ComputeHash(Encoding.ASCII.GetBytes(String.Concat(address, "-", i)));

                for (int h = 0; h < partCount; h++)
                {
                    k[i * partCount + h] = BitConverter.ToUInt32(data, h * keyLength);
                }
            }

            return k;
        }

        private void BuildMasterPrimaryAndBackup()
        {
            _masterKeys.Clear();
            _keyToServer.Clear();
            _keyToBackup.Clear();

            int numNodes = _allServers.Count;
            if (numNodes == 0)
            {
                return;
            }

            int keysPerServer = _keys.Length / numNodes;
            for (int i = 0; i < numNodes; i++)
            {
                int start = i * keysPerServer;
                int end = (i == numNodes - 1) ? _keys.Length : start + keysPerServer;
                for (int j = start; j < end; j++)
                {
                    _masterKeys[_keys[j]] = _allServers[i];
                    _keyToServer[_keys[j]] = _allServers[i];
                    _keyToBackup[_keys[j]] = FindNextNodeForBackup(_allServers[i]);
                }
            }
        }

        private void ReBuildMaster()
        {
            _masterKeys.Clear();

            int numNodes = _allServers.Count;
            if (numNodes == 0)
            {
                return;
            }

            int keysPerServer = _keys.Length / numNodes;
            for (int i = 0; i < numNodes; i++)
            {
                int start = i * keysPerServer;
                int end = (i == numNodes - 1) ? _keys.Length : start + keysPerServer;
                for (int j = start; j < end; j++)
                {
                    _masterKeys[_keys[j]] = _allServers[i];
                }
            }
        }
    }
}
