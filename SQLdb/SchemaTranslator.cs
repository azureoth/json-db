using Azureoth.Modules.SQLdb.Datastructures.Schema;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Azureoth.Modules.SQLdb
{
    public class SchemaTranslator
    {
        private static Dictionary<string, string> ValidFieldTypeRegexes = new Dictionary<string, string>
        {
            { @"^Number$", "int" },
            { @"BigNumber$", "bigint" },
            { @"Decimal$", "float(53)" },
            { @"Characters$|Characters\((\d+\))$", "nvarchar" },
            { @"Text$", "text" },
            { @"Boolean$", "bit" },
            { @"DateTime$", "datetime" },
            { @"Date$", "date" },
            { @"^Time$", "time" },
            { @"Binary$|Binary\((\d+\))$", "varbinary" },
            { @"Array\((\w+)\)$", "-TABLE" },
            { @"MTM\((\w+)\)$", "-THROUGH" },
        };

        public SQLSchema JsonToSQL(Dictionary<string, JsonTable> Schema, string DatabaseName, string SchemaName, bool CreateSchema = true)
        {
            var SqlSchema = new SQLSchema()
            {
                OriginalJson = JsonConvert.SerializeObject(Schema)
            };

            ValidateSchemaAndFormatTypes(Schema);

            SqlSchema.SchemaCreateSQL = $"Create Schema {SchemaName};";
            SqlSchema.RawSQL = "";

            //Dependent Table, Principal Table, PK Fields Name:Type, FK Name
            var OneToManyRelations = new List<Tuple<string, string, Dictionary<string, string>, string>>();

            //Table 1, Table 2, Table 1 PK  Fields Name:Type, Table 2 PK  Fields Name:Type, FK Name
            var ManyToManyRelations = new List<Tuple<string, string, Dictionary<string, string>, Dictionary<string, string>, string>>();

            //Parse Relations
            foreach (var table in Schema)
            {
                var foreignRelations = table.Value.Fields.Where(f => f.Value.Contains("-TABLE"));

                foreach (var foreigRelation in foreignRelations)
                {
                    var tableWithFk = Schema.SingleOrDefault(t => t.Key == InnerValue(foreigRelation.Value));

                    if (tableWithFk.Equals(default(KeyValuePair<string, JsonTable>)))
                        throw new ArgumentException($"Table {InnerValue(foreigRelation.Value)} used in Array does not exist");

                    var ForeignPks = new Dictionary<string, string>();
                    if (table.Value?.Meta?.PrimaryKeyColumns == null || !table.Value.Meta.PrimaryKeyColumns.Any())
                    {
                        ForeignPks.Add("Id", "int");
                    }
                    else
                    {
                        var pkFields = table.Value.Fields.Where(f => table.Value.Meta.PrimaryKeyColumns.Contains(f.Key));
                        foreach (var pkField in pkFields)
                        {
                            ForeignPks.Add(pkField.Key, pkField.Value);
                        }
                    }

                    OneToManyRelations.Add(new Tuple<string, string, Dictionary<string, string>, string>
                        (tableWithFk.Key, table.Key, ForeignPks, foreigRelation.Key));
                }


                var throughRelations = table.Value.Fields.Where(f => f.Value.Contains("-THROUGH"));
                foreach (var throughRelation in throughRelations)
                {
                    var otherTable = Schema.SingleOrDefault(t => t.Key == InnerValue(throughRelation.Value));

                    if (otherTable.Equals(default(KeyValuePair<string, JsonTable>)))
                        throw new ArgumentException($"Table {InnerValue(throughRelation.Value)} used in MTM does not exist");

                    var ForeignPkSet1 = new Dictionary<string, string>();
                    var ForeignPkSet2 = new Dictionary<string, string>();

                    if (table.Value?.Meta?.PrimaryKeyColumns == null || !table.Value.Meta.PrimaryKeyColumns.Any())
                    {
                        ForeignPkSet1.Add("Id", "int");
                    }
                    else
                    {
                        var pkFields = table.Value.Fields.Where(f => table.Value.Meta.PrimaryKeyColumns.Contains(f.Key));
                        foreach (var pkField in pkFields)
                        {
                            ForeignPkSet1.Add(pkField.Key, pkField.Value);
                        }
                    }

                    if (otherTable.Value?.Meta?.PrimaryKeyColumns == null || !otherTable.Value.Meta.PrimaryKeyColumns.Any())
                    {
                        ForeignPkSet2.Add("Id", "int");
                    }
                    else
                    {
                        var pkFields = otherTable.Value.Fields.Where(f => otherTable.Value.Meta.PrimaryKeyColumns.Contains(f.Key));
                        foreach (var pkField in pkFields)
                        {
                            ForeignPkSet2.Add(pkField.Key, pkField.Value);
                        }
                    }

                    ManyToManyRelations.Add(new Tuple<string, string, Dictionary<string, string>, Dictionary<string, string>, string>
                        (otherTable.Key, table.Key, ForeignPkSet1, ForeignPkSet2, throughRelation.Key));
                }
            }

            //Create tables
            foreach (var table in Schema)
            {
                SqlSchema.RawSQL += $"Create TABLE {SchemaName}.{table.Key} ( ";

                //Add default PK if none specified
                if (table.Value?.Meta?.PrimaryKeyColumns == null || !table.Value.Meta.PrimaryKeyColumns.Any())
                {
                    SqlSchema.RawSQL += "Id int IDENTITY(1,1) PRIMARY KEY, ";
                }

                //Add date tracked columns
                SqlSchema.RawSQL += "CreatedOn datetime, ";
                SqlSchema.RawSQL += "LastModifiedOn datetime, ";

                //Add concrete fields
                foreach (var field in table.Value.Fields.Where(f => !f.Value.Contains("-TABLE") && !f.Value.Contains("-THROUGH")))
                {
                    SqlSchema.RawSQL += $"{field.Key} {field.Value} ";

                    if ((table.Value?.Meta?.RequiredColumns != null &&
                        table.Value.Meta.RequiredColumns.Contains(field.Key)) ||
                        (table.Value?.Meta?.PrimaryKeyColumns != null &&
                        table.Value.Meta.PrimaryKeyColumns.Contains(field.Key)))
                    {
                        SqlSchema.RawSQL += "NOT NULL ";
                    }

                    SqlSchema.RawSQL += ", ";
                }

                //Add Foreign Key fields
                //FkIndex required to ensure multiple OneToMany Relations between the same two tables work
                var foreignKeyFields = OneToManyRelations.Where(r => r.Item1 == table.Key);

                foreach (var fk in foreignKeyFields)
                {
                    foreach (var fkType in fk.Item3)
                    {
                        SqlSchema.RawSQL += $"{fk.Item2}_{fkType.Key}_{fk.Item4} {fkType.Value}, ";
                    }
                }

                //Add PK constraint if specified in meta
                if (table.Value?.Meta?.PrimaryKeyColumns != null && table.Value.Meta.PrimaryKeyColumns.Any())
                {
                    SqlSchema.RawSQL += $"Constraint {table.Key}_PK PRIMARY KEY ( ";
                    foreach (var pk in table.Value.Meta.PrimaryKeyColumns)
                    {
                        SqlSchema.RawSQL += $"{pk}, ";
                    }
                    SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                    SqlSchema.RawSQL += ")  ";
                }

                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += "); ";
            }

            //Create Foreign Key Constraints
            foreach (var relation in OneToManyRelations)
            {
                SqlSchema.RawSQL += $"ALTER TABLE {SchemaName}.{relation.Item1} Add Constraint {relation.Item1}_{relation.Item2}_{relation.Item4}_FK Foreign Key (";
                foreach (var fkType in relation.Item3)
                {
                    SqlSchema.RawSQL += $"{relation.Item2}_{fkType.Key}_{relation.Item4}, ";
                }

                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += $") References {SchemaName}.{relation.Item2}(";

                foreach (var fkType in relation.Item3)
                {
                    SqlSchema.RawSQL += $"{fkType.Key}, ";
                }
                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += "); ";
            }


            //Create Through Tables
            foreach (var relation in ManyToManyRelations)
            {
                SqlSchema.RawSQL += $"Create TABLE {SchemaName}.{relation.Item1}_{relation.Item2}_{relation.Item5} ( ";

                //Foreign key fields for table 1
                foreach (var fkType in relation.Item3)
                {
                    SqlSchema.RawSQL += $"{relation.Item2}_{fkType.Key} {fkType.Value} NOT NULL, ";
                }

                //Foreign key fields for table 2
                foreach (var fkType in relation.Item4)
                {
                    SqlSchema.RawSQL += $"{relation.Item1}_{fkType.Key} {fkType.Value} NOT NULL, ";
                }

                //Add PK constraint on all the fields 
                SqlSchema.RawSQL += $"Constraint {relation.Item1}_{relation.Item2}_{relation.Item5}_PK PRIMARY KEY ( ";
                foreach (var fkType in relation.Item3)
                {
                    SqlSchema.RawSQL += $"{relation.Item2}_{fkType.Key}, ";
                }
                foreach (var fkType in relation.Item4)
                {
                    SqlSchema.RawSQL += $"{relation.Item1}_{fkType.Key}, ";
                }
                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += ")  ";

                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += $"); ";


                //Foreign key constraints for table 1
                SqlSchema.RawSQL += $"ALTER TABLE {SchemaName}.{relation.Item1}_{relation.Item2}_{relation.Item5} Add Constraint {relation.Item1}_{relation.Item2}_{relation.Item5}_FK Foreign Key (";
                foreach (var fkType in relation.Item3)
                {
                    SqlSchema.RawSQL += $"{relation.Item2}_{fkType.Key}, ";
                }

                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += $") References {SchemaName}.{relation.Item2}(";

                foreach (var fkType in relation.Item3)
                {
                    SqlSchema.RawSQL += $"{fkType.Key}, ";
                }
                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += "); ";


                //Foreign key constraints for table 2
                SqlSchema.RawSQL += $"ALTER TABLE {SchemaName}.{relation.Item1}_{relation.Item2}_{relation.Item5} Add Constraint {relation.Item2}_{relation.Item1}_{relation.Item5}_FK Foreign Key  (";
                foreach (var fkType in relation.Item4)
                {
                    SqlSchema.RawSQL += $"{relation.Item1}_{fkType.Key}, ";
                }

                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += $") References {SchemaName}.{relation.Item1}(";

                foreach (var fkType in relation.Item4)
                {
                    SqlSchema.RawSQL += $"{fkType.Key}, ";
                }
                SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                SqlSchema.RawSQL += "); ";
            }

            //Create indices specified in Meta for tables
            foreach (var table in Schema)
            {
                if (table.Value?.Meta?.Indices == null || !table.Value.Meta.Indices.Any())
                    continue;

                foreach (var index in table.Value.Meta.Indices.Select((i, loopIndex) => new { LoopIndex = loopIndex, Index = i }))
                {
                    var unique = index.Index.IsUnique ? "Unique" : "";
                    SqlSchema.RawSQL += $"Create {unique} Index {table.Key}_{index.LoopIndex} On {SchemaName}.{table.Key} (";
                    foreach (var indexedColumn in index.Index.IndexedColumns)
                    {
                        var order = indexedColumn.Value ? "ASC" : "DESC";
                        SqlSchema.RawSQL += $"{indexedColumn.Key} {order}, ";
                    }

                    SqlSchema.RawSQL = SqlSchema.RawSQL.Substring(0, SqlSchema.RawSQL.Length - 2);
                    SqlSchema.RawSQL += ");";
                }

            }

            return SqlSchema;
        }


        private static void ValidateSchemaAndFormatTypes(Dictionary<string, JsonTable> schema)
        {

            if (schema == null || !schema.Any())
                throw new ArgumentException("No tables defined");

            if (schema.Select(t => t.Value).Count() != schema.Select(t => t.Value).Distinct().Count())
                throw new ArgumentException("Multiple tables share the same name");

            foreach (var table in schema)
            {
                if (table.Value?.Fields == null || !table.Value.Fields.Any())
                    throw new ArgumentException($"No fields defined in table {table.Key}");

                var definedColumns = table.Value.Fields.Select(t => t.Key);

                if (table.Value.Fields.Select(t => t.Key).Count() != table.Value.Fields.Select(t => t.Key).Distinct().Count())
                    throw new ArgumentException($"Multiple fields share the same name in table {table.Key}");

                if (table.Value?.Meta?.RequiredColumns != null &&
                    table.Value.Meta.RequiredColumns.Any(r => !definedColumns.Contains(r)))
                    throw new ArgumentException($"Required Column is not defined in table {table.Key}");

                if (table.Value?.Meta?.PrimaryKeyColumns != null &&
                    table.Value.Meta.PrimaryKeyColumns.Any(p => !definedColumns.Contains(p)))
                    throw new ArgumentException($"Primary Key Column is not defined in table {table.Key}");

                if (table.Value?.Meta?.Indices != null &&
                    table.Value.Meta.Indices.Any(i => i.IndexedColumns.Any(c => !definedColumns.Contains(c.Key))))
                    throw new ArgumentException($"Indexed Column is not defined in table {table.Key}");

                var oldFields = new Dictionary<string, string>(table.Value.Fields);
                foreach (var field in oldFields)
                {
                    var matchingRegex = ValidFieldTypeRegexes.Select(r =>
                    new { Regex = r, Match = Regex.Match(field.Value, r.Key) }).SingleOrDefault(m => m.Match.Success);

                    if (matchingRegex == null)
                        throw new ArgumentException($"Field type is not supported in table {table.Key}");

                    var newFieldValue = matchingRegex.Regex.Value;

                    if (matchingRegex.Match.Groups[1].Success)
                    {
                        newFieldValue += $"({matchingRegex.Match.Groups[1].Value})";
                    }

                    table.Value.Fields[field.Key] = newFieldValue;
                }
            }
        }

        private static string InnerValue(string ParsedValue)
        {
            return ParsedValue.Substring(ParsedValue.IndexOf('(') + 1, ParsedValue.IndexOf(')') - ParsedValue.IndexOf('(') - 1);
        }
    }
}
