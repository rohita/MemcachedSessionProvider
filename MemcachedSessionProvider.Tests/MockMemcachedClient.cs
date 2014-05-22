using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

namespace MemcachedSessionProvider.Tests
{
    public class MockMemcachedClient : IMemcachedClient
    {
        private SessionNodeLocator _locator;

        private Dictionary<IMemcachedNode, Dictionary<string, object>> _data;

        public MockMemcachedClient(IList<IMemcachedNode> nodes)
        {
            _data = new Dictionary<IMemcachedNode, Dictionary<string, object>>();
            foreach (var node in nodes)
            {
                _data.Add(node, new Dictionary<string, object>());
            }
            
            _locator = new SessionNodeLocator();
            _locator.Initialize(nodes);
        }

        public T Get<T>(string key)
        {
            var node = _locator.Locate(key);
            Console.WriteLine("Get => {0}:{1}", key, node == null? "null" : node.EndPoint.ToString());

            if (node == null 
                || _data[node] == null 
                || !_data[node].ContainsKey(key)) 
                return default(T); 

            return (T)_data[node][key]; 
        }

        public bool Store(StoreMode mode, string key, object value, TimeSpan validFor)
        {
            var node = _locator.Locate(key);
            Console.WriteLine("Store => {0}:{1}", key, node == null ? "null" : node.EndPoint.ToString());

            _data[node][key] = value;
            return true; 
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public void SetNodeDead(IMemcachedNode n1, List<IMemcachedNode> activeNodes)
        {
            Console.WriteLine("Down => {0}", n1.EndPoint);

            // Having to use reflection to set a private field
            var prop = n1.GetType().GetField("internalPoolImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            var internalPoolImpl = prop.GetValue(n1);
            var prop2 = internalPoolImpl.GetType().GetField("isAlive", BindingFlags.NonPublic | BindingFlags.Instance);
            prop2.SetValue(internalPoolImpl, false);

            _data[n1] = null; 
            _locator = new SessionNodeLocator();
            _locator.Initialize(activeNodes);
        }

        public void SetNodeAlive(IMemcachedNode n1, List<IMemcachedNode> activeNodes)
        {
            Console.WriteLine("Up => {0}", n1.EndPoint);

            // Having to use reflection to set a private field
            var prop = n1.GetType().GetField("internalPoolImpl", BindingFlags.NonPublic | BindingFlags.Instance);
            var internalPoolImpl = prop.GetValue(n1);
            var prop2 = internalPoolImpl.GetType().GetField("isAlive", BindingFlags.NonPublic | BindingFlags.Instance);
            prop2.SetValue(internalPoolImpl, true);

            _data[n1] = new Dictionary<string, object>();
            _locator.Initialize(activeNodes);
        }

        public void Dispose()
        {
            
        }

        #region Not Implemented
        

        public object Get(string key)
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, object> Get(IEnumerable<string> keys)
        {
            throw new NotImplementedException();
        }

        public bool TryGet(string key, out object value)
        {
            throw new NotImplementedException();
        }

        public bool TryGetWithCas(string key, out CasResult<object> value)
        {
            throw new NotImplementedException();
        }

        public CasResult<object> GetWithCas(string key)
        {
            throw new NotImplementedException();
        }

        public CasResult<T> GetWithCas<T>(string key)
        {
            throw new NotImplementedException();
        }

        public IDictionary<string, CasResult<object>> GetWithCas(IEnumerable<string> keys)
        {
            throw new NotImplementedException();
        }

        public bool Append(string key, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public CasResult<bool> Append(string key, ulong cas, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public bool Prepend(string key, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public CasResult<bool> Prepend(string key, ulong cas, ArraySegment<byte> data)
        {
            throw new NotImplementedException();
        }

        public bool Store(StoreMode mode, string key, object value)
        {
            throw new NotImplementedException();
        }

        public bool Store(StoreMode mode, string key, object value, DateTime expiresAt)
        {
            throw new NotImplementedException();
        }

        public CasResult<bool> Cas(StoreMode mode, string key, object value)
        {
            throw new NotImplementedException();
        }

        public CasResult<bool> Cas(StoreMode mode, string key, object value, ulong cas)
        {
            throw new NotImplementedException();
        }

        public CasResult<bool> Cas(StoreMode mode, string key, object value, DateTime expiresAt, ulong cas)
        {
            throw new NotImplementedException();
        }

        public CasResult<bool> Cas(StoreMode mode, string key, object value, TimeSpan validFor, ulong cas)
        {
            throw new NotImplementedException();
        }

        public ulong Decrement(string key, ulong defaultValue, ulong delta)
        {
            throw new NotImplementedException();
        }

        public ulong Decrement(string key, ulong defaultValue, ulong delta, DateTime expiresAt)
        {
            throw new NotImplementedException();
        }

        public ulong Decrement(string key, ulong defaultValue, ulong delta, TimeSpan validFor)
        {
            throw new NotImplementedException();
        }

        public CasResult<ulong> Decrement(string key, ulong defaultValue, ulong delta, ulong cas)
        {
            throw new NotImplementedException();
        }

        public CasResult<ulong> Decrement(string key, ulong defaultValue, ulong delta, DateTime expiresAt, ulong cas)
        {
            throw new NotImplementedException();
        }

        public CasResult<ulong> Decrement(string key, ulong defaultValue, ulong delta, TimeSpan validFor, ulong cas)
        {
            throw new NotImplementedException();
        }

        public ulong Increment(string key, ulong defaultValue, ulong delta)
        {
            throw new NotImplementedException();
        }

        public ulong Increment(string key, ulong defaultValue, ulong delta, DateTime expiresAt)
        {
            throw new NotImplementedException();
        }

        public ulong Increment(string key, ulong defaultValue, ulong delta, TimeSpan validFor)
        {
            throw new NotImplementedException();
        }

        public CasResult<ulong> Increment(string key, ulong defaultValue, ulong delta, ulong cas)
        {
            throw new NotImplementedException();
        }

        public CasResult<ulong> Increment(string key, ulong defaultValue, ulong delta, DateTime expiresAt, ulong cas)
        {
            throw new NotImplementedException();
        }

        public CasResult<ulong> Increment(string key, ulong defaultValue, ulong delta, TimeSpan validFor, ulong cas)
        {
            throw new NotImplementedException();
        }

        public void FlushAll()
        {
            throw new NotImplementedException();
        }

        public ServerStats Stats()
        {
            throw new NotImplementedException();
        }

        public ServerStats Stats(string type)
        {
            throw new NotImplementedException();
        }

        public event Action<IMemcachedNode> NodeFailed;

        #endregion
    }
}
