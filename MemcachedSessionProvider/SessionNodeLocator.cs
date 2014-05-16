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

using System.Collections.Generic;
using Enyim.Caching.Memcached;

namespace MemcachedSessionProvider
{
    /// <summary>
    /// This is a custom implementation of the <see cref="IMemcachedNodeLocator "/> interface 
    /// in the Enyim.Caching.Memcached library. This handles keys with prefix "bak:". 
    /// These backup keys are stored on the "next" available server. 
    /// </summary>
    internal class SessionNodeLocator : IMemcachedNodeLocator
    {

        void IMemcachedNodeLocator.Initialize(IList<IMemcachedNode> nodes)
        {
            SessionNodeLocatorImpl.Instance.Initialize(nodes);
        }

        IMemcachedNode IMemcachedNodeLocator.Locate(string key)
        {
            return SessionNodeLocatorImpl.Instance.Locate(key);
        }

        IEnumerable<IMemcachedNode> IMemcachedNodeLocator.GetWorkingNodes()
        {
            return SessionNodeLocatorImpl.Instance.GetWorkingNodes();
        }
        
    }
}
