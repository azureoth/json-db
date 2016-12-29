using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azureoth.Modules.SQLdb.Datastructures.Schema
{
    public class JsonTableMeta
    {
        public IEnumerable<string> PrimaryKeyColumns;
        public IEnumerable<string> RequiredColumns;
        public IEnumerable<JsonIndex> Indices;
    }
}
