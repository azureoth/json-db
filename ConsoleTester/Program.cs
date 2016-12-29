using Azureoth.Modules.SQLdb;
using Azureoth.Modules.SQLdb.Datastructures.Schema;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleTester
{
    public class Program
    {
        public static void Main(string[] args)
        {

            Console.Out.WriteLine("Please enter path to json file: ");
            var path = Console.In.ReadLine();

            SchemaBuilderFactory factory = new SchemaBuilderFactory();
            var schemaBuilder = factory.CreateSchemaBuilder("Server=(localdb)\\MSSQLLocalDB;Database=Hacking_Dev;", "Hacking_Dev");

            var text = File.ReadAllText(path);
            var result = JsonConvert.DeserializeObject<Dictionary<string, JsonTable>>(text);

            schemaBuilder.UpdateSchema(result, result, "llal").GetAwaiter().GetResult();
        }
    }
}
