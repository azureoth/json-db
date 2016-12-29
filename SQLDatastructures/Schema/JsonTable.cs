using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azureoth.Modules.SQLdb.Datastructures.Schema
{
    public class JsonTable
    {
        public JsonTableMeta Meta;
        public Dictionary<string, string> Fields;
    }
}
