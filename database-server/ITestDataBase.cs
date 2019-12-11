using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace database_server
{
    interface ITestDataBase
    {
        public Task<bool> Add(String Key, String Value);
        public Task<String?> Get(String Key);
    }
}
