using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azureoth.Modules.SQLdb.Datastructures.Schema
{
    public class JsonIndex
    {
        public Dictionary<string, bool> IndexedColumns;

        public bool IsUnique;
    }
}
