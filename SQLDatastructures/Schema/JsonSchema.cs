using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azureoth.Modules.SQLdb.Datastructures.Schema
{
    public class JsonDatabaseSchema
    {
        public string DatabaseName;
        public Dictionary<string, JsonTable> Tables { get; set; }
    }
}
