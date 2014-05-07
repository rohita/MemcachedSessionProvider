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
using System.Web;

namespace MemcachedSessionProvider
{
    internal class SessionKeyFormat
    {

        private string _applicationName;
        private const string AspSessionPrefix = "__AspSession_"; 
        private const string BackupPrefix = "bak:";
        private const string Format = "{0}{1}{2}_{3}";

        public SessionKeyFormat(string applicationName)
        {
            if (string.IsNullOrEmpty(applicationName))
                applicationName = HttpRuntime.AppDomainAppId;
            _applicationName = applicationName; 
        }

        public String GetBackupKey(String key)
        {
            if (IsBackupKey(key))
            {
                return key;
            }

            if (IsPrimaryKey(key))
            {
                return String.Format("{0}{1}", BackupPrefix, key); 
            }

            return String.Format(Format, BackupPrefix, AspSessionPrefix, _applicationName, key); 
        }

        public bool IsBackupKey(String key)
        {
            return key.StartsWith(BackupPrefix); 
        }

        public bool IsPrimaryKey(string key)
        {
            return key.StartsWith(AspSessionPrefix); 
        }

        public String GetPrimaryKey(String key)
        {
            if (IsPrimaryKey(key))
            {
                return key;
            }

            if (IsBackupKey(key))
            {
                return key.Substring(BackupPrefix.Length);
            }

            return String.Format(Format, string.Empty, AspSessionPrefix, _applicationName, key); 
        }
    }
}
