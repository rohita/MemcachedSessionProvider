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
using System.Text;
using System.Threading;
using Enyim;
using Enyim.Caching;
using Enyim.Caching.Memcached;

namespace MemcachedSessionProvider
{
    /// <summary>
    /// This is a slight modification of the <see cref="DefaultNodeLocator "/> in the Enyim.Caching.Memcached
    /// library. It is extended to handle keys with prefix "bak:". These backup keys are 
    /// stored on the "next" available server. 
    /// </summary>
    internal class BackupEnabledNodeLocator : IMemcachedNodeLocator, IDisposable
    {
        private const int ServerAddressMutations = 100;

		// holds all server keys for mapping an item key to the server consistently
		private uint[] _keys;
		// used to lookup a server based on its key
		private Dictionary<uint, IMemcachedNode> _servers;
		private Dictionary<IMemcachedNode, bool> _deadServers;
		private List<IMemcachedNode> _allServers;
		private ReaderWriterLockSlim _serverAccessLock;
        private SessionKeyFormat _sessionKeyFormat; 

        public BackupEnabledNodeLocator()
		{
			this._servers = new Dictionary<uint, IMemcachedNode>(new UIntEqualityComparer());
			this._deadServers = new Dictionary<IMemcachedNode, bool>();
			this._allServers = new List<IMemcachedNode>();
			this._serverAccessLock = new ReaderWriterLockSlim();
            this._sessionKeyFormat = new SessionKeyFormat(null);
		}

		private void BuildIndex(List<IMemcachedNode> nodes)
		{
            var keys = new uint[nodes.Count * BackupEnabledNodeLocator.ServerAddressMutations];

			int nodeIdx = 0;

			foreach (IMemcachedNode node in nodes)
			{
                var tmpKeys = BackupEnabledNodeLocator.GenerateKeys(node, BackupEnabledNodeLocator.ServerAddressMutations);

				for (var i = 0; i < tmpKeys.Length; i++)
				{
					this._servers[tmpKeys[i]] = node;
				}

				tmpKeys.CopyTo(keys, nodeIdx);
                nodeIdx += BackupEnabledNodeLocator.ServerAddressMutations;
			}

			Array.Sort<uint>(keys);
			Interlocked.Exchange(ref this._keys, keys);
		}

		void IMemcachedNodeLocator.Initialize(IList<IMemcachedNode> nodes)
		{
			this._serverAccessLock.EnterWriteLock();

			try
			{
				this._allServers = nodes.ToList();
				this.BuildIndex(this._allServers);
			}
			finally
			{
				this._serverAccessLock.ExitWriteLock();
			}
		}

		IMemcachedNode IMemcachedNodeLocator.Locate(string key)
		{
			if (key == null) throw new ArgumentNullException("key");

			this._serverAccessLock.EnterUpgradeableReadLock();

			try { return this.Locate(key); }
			finally { this._serverAccessLock.ExitUpgradeableReadLock(); }
		}

		IEnumerable<IMemcachedNode> IMemcachedNodeLocator.GetWorkingNodes()
		{
			this._serverAccessLock.EnterReadLock();

			try { return this._allServers.Except(this._deadServers.Keys).ToArray(); }
			finally { this._serverAccessLock.ExitReadLock(); }
		}

		private IMemcachedNode Locate(string key)
		{
			var node = FindNode(key);

            if (_sessionKeyFormat.IsBackupKey(key))
            {
                node = GetBackupNode(node); 
            }

			if (node == null || node.IsAlive)
				return node;

			// move the current node to the dead list and rebuild the indexes
			this._serverAccessLock.EnterWriteLock();

			try
			{
				// check if it's still dead or it came back
				// while waiting for the write lock
				if (!node.IsAlive)
					this._deadServers[node] = true;

				this.BuildIndex(this._allServers.Except(this._deadServers.Keys).ToList());
			}
			finally
			{
				this._serverAccessLock.ExitWriteLock();
			}

			// try again with the dead server removed from the lists
			return this.Locate(key);
		}

		/// <summary>
		/// locates a node by its key
		/// </summary>
        /// <param name="rawKey"></param>
		/// <returns></returns>
		private IMemcachedNode FindNode(string rawKey)
		{
			if (this._keys.Length == 0) return null;

            // clean the key from any backup prefix
            string key = _sessionKeyFormat.GetPrimaryKey(rawKey); 

			uint itemKeyHash = BitConverter.ToUInt32(new FNV1a().ComputeHash(Encoding.UTF8.GetBytes(key)), 0);
			// get the index of the server assigned to this hash
			int foundIndex = Array.BinarySearch<uint>(this._keys, itemKeyHash);

			// no exact match
			if (foundIndex < 0)
			{
				// this is the nearest server in the list
				foundIndex = ~foundIndex;

				if (foundIndex == 0)
				{
					// it's smaller than everything, so use the last server (with the highest key)
					foundIndex = this._keys.Length - 1;
				}
				else if (foundIndex >= this._keys.Length)
				{
					// the key was larger than all server keys, so return the first server
					foundIndex = 0;
				}
			}

			if (foundIndex < 0 || foundIndex > this._keys.Length)
				return null;

			return this._servers[this._keys[foundIndex]];
		}

        /// <summary>
        /// Get the next available node for the given one. For the last node
        /// the first one is returned. If this list contains only a 
        /// single node, conceptionally there's no next node, so null 
        /// is returned.
        /// </summary>
        private IMemcachedNode GetBackupNode(IMemcachedNode primaryNode)
        {
            if (primaryNode == null || this._allServers.Count == 1)
            {
                return null; 
            }

            var backupNode = primaryNode; 
            do
            {
                var idx = _allServers.FindIndex(v=> v.EndPoint.Equals(backupNode.EndPoint));
                var nextIdx = (idx == _allServers.Count - 1) ? 0 : idx + 1;
                backupNode = _allServers[nextIdx];
                if (backupNode.EndPoint.Equals(primaryNode.EndPoint))
                {
                    backupNode = null;
                }

            } while (backupNode != null && _deadServers.ContainsKey(backupNode));


            return backupNode;
        }



		private static uint[] GenerateKeys(IMemcachedNode node, int numberOfKeys)
		{
			const int KeyLength = 4;
			const int PartCount = 1; // (ModifiedFNV.HashSize / 8) / KeyLength; // HashSize is in bits, uint is 4 byte long

			var k = new uint[PartCount * numberOfKeys];

			// every server is registered numberOfKeys times
			// using UInt32s generated from the different parts of the hash
			// i.e. hash is 64 bit:
			// 00 00 aa bb 00 00 cc dd
			// server will be stored with keys 0x0000aabb & 0x0000ccdd
			// (or a bit differently based on the little/big indianness of the host)
			string address = node.EndPoint.ToString();
			var fnv = new FNV1a();

			for (int i = 0; i < numberOfKeys; i++)
			{
				byte[] data = fnv.ComputeHash(Encoding.ASCII.GetBytes(String.Concat(address, "-", i)));

				for (int h = 0; h < PartCount; h++)
				{
					k[i * PartCount + h] = BitConverter.ToUInt32(data, h * KeyLength);
				}
			}

			return k;
		}

		#region [ IDisposable                  ]

		void IDisposable.Dispose()
		{
			using (this._serverAccessLock)
			{
				this._serverAccessLock.EnterWriteLock();

				try
				{
					// kill all pending operations (with an exception)
					// it's not nice, but disposeing an instance while being used is bad practice
					this._allServers = null;
					this._servers = null;
					this._keys = null;
					this._deadServers = null;
				}
				finally
				{
					this._serverAccessLock.ExitWriteLock();
				}
			}

			this._serverAccessLock = null;
		}

		#endregion
    }
}
