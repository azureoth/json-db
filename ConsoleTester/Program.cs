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
            SchemaBuilderFactory factory = new SchemaBuilderFactory();
            var schemaBuilder = factory.CreateSchemaBuilder("Server=(localdb)\\MSSQLLocalDB;Database=Hacking_Prod;", "Server=(localdb)\\MSSQLLocalDB;Database=Hacking_Dev;", "C:/Logs/");

            Console.Out.WriteLine("Name of App");
            var name = Console.In.ReadLine();

            Console.Out.WriteLine("Create or Update? (c/u)");
            var res = Console.In.ReadLine();
            if (res == "c")
            {
                Console.Out.WriteLine("Please enter path to json file: ");
                var p = Console.In.ReadLine();
                var t = File.ReadAllText(p);
                var r = JsonConvert.DeserializeObject<Dictionary<string, JsonTable>>(t);
                schemaBuilder.CreateSchema(r, name).GetAwaiter().GetResult();
            }
            else
            {
                Console.Out.WriteLine("Please enter path to old json file: ");
                var path = Console.In.ReadLine();
                var text = File.ReadAllText(path);
                var result = JsonConvert.DeserializeObject<Dictionary<string, JsonTable>>(text);

                Console.Out.WriteLine("Please enter path to new json file: ");
                var path2 = Console.In.ReadLine();
                var text2 = File.ReadAllText(path2);
                var result2 = JsonConvert.DeserializeObject<Dictionary<string, JsonTable>>(text2);

                Console.Out.WriteLine("Use Force to ignore errors? (y/n)");
                var force = Console.In.ReadLine();

                schemaBuilder.UpdateSchema(result, result2, name, force == "y").GetAwaiter().GetResult();
            }
        }
    }
}
