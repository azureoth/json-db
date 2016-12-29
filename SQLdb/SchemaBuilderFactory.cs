using Azureoth.Modules.SQLdb.Datastructures.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azureoth.Modules.SQLdb.Datastructures;

namespace Azureoth.Modules.SQLdb
{
    public class SchemaBuilderFactory : ISchemaBuilderFactory
    {
        public ISchemaBuilder CreateSchemaBuilder(string DatabaseConnectionString, string DatabaseName)
        {
            return new SchemaBuilder(DatabaseName, DatabaseConnectionString);
        }
    }
}
