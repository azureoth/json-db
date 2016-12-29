using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azureoth.Modules.SQLdb.Datastructures.Schema
{
    public interface ISchemaBuilderFactory
    {
        ISchemaBuilder CreateSchemaBuilder(string DatabaseConnectionString, string DatabaseName);
    }
}
