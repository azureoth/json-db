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
        public ISchemaBuilder CreateSchemaBuilder(string PrimaryDbConnection, string SecondaryDbConnection, string TempFolderPath)
        {
            return new SchemaBuilder(PrimaryDbConnection, SecondaryDbConnection, TempFolderPath);
        }
    }
}
