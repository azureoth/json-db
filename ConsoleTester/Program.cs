using Azureoth.Modules.SQLdb;
using Azureoth.Modules.SQLdb.Datastructures.Schema;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

            string text = System.IO.File.ReadAllText(path);

            var json = JsonConvert.DeserializeObject<Dictionary<string, JsonTable>>(text);

            var translator = new SchemaTranslator();

            var result = translator.JsonToSQL(json, "Hacking_Dev", "MySuperSecretSchema");

            System.IO.File.WriteAllText(path.Replace(".json", ".sql"), result.RawSQL);
        }
    }
}
