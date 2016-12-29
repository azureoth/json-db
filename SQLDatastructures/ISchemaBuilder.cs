using Azureoth.Modules.SQLdb.Datastructures.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azureoth.Modules.SQLdb.Datastructures
{
    public interface ISchemaBuilder
    {
        Task UpdateSchema(Dictionary<string, JsonTable> OldSchema, Dictionary<string, JsonTable> NewSchema, string AppName, bool ForceFlag = false);

        Task CreateSchema(Dictionary<string, JsonTable> NewSchema, string AppName);
    }
}
