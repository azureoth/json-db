using Azureoth.Modules.SQLdb.Datastructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azureoth.Modules.SQLdb.Datastructures.Schema;
using System.Data.SqlClient;
using System.IO;
using System.Diagnostics;

namespace Azureoth.Modules.SQLdb
{
    public class SchemaBuilder : ISchemaBuilder
    {
        private string stageDatabaseName;
        private string prodDatabaseName;
        private SqlConnection prodConnection;
        private SqlConnection stageConnection;
        private SchemaTranslator translator;
        private string TempFolderBasePath;
        private string SqlPackageUtililtyPath;
        private string SqlCommandUtililtyPath;

        internal SchemaBuilder(string primaryDatabaseConnection, string secondaryDatabaseConnection, string TempFolderPath)
        {
            prodConnection = new SqlConnection(primaryDatabaseConnection);
            prodDatabaseName = ExtractDatabaseNameFromConnectionString(primaryDatabaseConnection);
            stageConnection = new SqlConnection(secondaryDatabaseConnection);
            stageDatabaseName = ExtractDatabaseNameFromConnectionString(secondaryDatabaseConnection);
            translator = new SchemaTranslator();
            TempFolderBasePath = TempFolderPath;
            SqlPackageUtililtyPath = "\"C:\\Program Files (x86)\\Microsoft SQL Server\\130\\DAC\\bin\\SqlPackage.exe\"";
            SqlCommandUtililtyPath = "\"C:\\Program Files\\Microsoft SQL Server\\110\\Tools\\Binn\\SQLCMD.EXE\"";
        }


        public async Task CreateSchema(Dictionary<string, JsonTable> NewSchema, string AppName)
        {
            await prodConnection.OpenAsync();

            var sqlSchema = translator.JsonToSQL(NewSchema, stageDatabaseName, AppName, false);

            //Apply new staging schema
            var createSchemaCommand = new SqlCommand(sqlSchema.SchemaCreateSQL);
            createSchemaCommand.Connection = prodConnection;
            await createSchemaCommand.ExecuteNonQueryAsync();

            //Create new tables for staging schema
            var applyTempSchemaCommand = new SqlCommand(sqlSchema.RawSQL);
            applyTempSchemaCommand.Connection = prodConnection;
            await applyTempSchemaCommand.ExecuteNonQueryAsync();

            prodConnection.Close();
        }

        public async Task UpdateSchema(Dictionary<string, JsonTable> OldSchema, Dictionary<string, JsonTable> NewSchema, string AppName, bool ForceFlag = false)
        {
            await stageConnection.OpenAsync();

            //Generate new staging schema
            var newGeneratedSchema = translator.JsonToSQL(NewSchema, stageDatabaseName, AppName, false);

            //Drop existing staging schema
            var dropExistingStageSchemaCommand = new SqlCommand($"exec CleanUpSchema '{AppName}', 'w'");
            dropExistingStageSchemaCommand.Connection = stageConnection;
            await dropExistingStageSchemaCommand.ExecuteNonQueryAsync();

            //Apply new staging schema
            var createSchemaCommand = new SqlCommand(newGeneratedSchema.SchemaCreateSQL);
            createSchemaCommand.Connection = stageConnection;
            await createSchemaCommand.ExecuteNonQueryAsync();

            //Create new tables for staging schema
            var applyTempSchemaCommand = new SqlCommand(newGeneratedSchema.RawSQL);
            applyTempSchemaCommand.Connection = stageConnection;
            await applyTempSchemaCommand.ExecuteNonQueryAsync();

            stageConnection.Close();

            //Package staging schema for comparison
            var stagingProcess = Process.Start($"{SqlPackageUtililtyPath}", $"/a:Extract /scs:{stageConnection.ConnectionString} " +
                $"/tf:{Path.Combine(TempFolderBasePath, AppName + "_Staging.dacpac")}");

            //Package prod schema for comparison
            var prodProcess = Process.Start($"{SqlPackageUtililtyPath}", $"/a:Extract /scs:{prodConnection.ConnectionString} " +
                $"/tf:{Path.Combine(TempFolderBasePath, AppName + ".dacpac")}");

            stagingProcess.WaitForExit();
            prodProcess.WaitForExit();

            //Generate migration sql
            var migrationPath = Path.Combine(TempFolderBasePath, AppName + "M.sql");
            string ExitIfDataLoss = ForceFlag ? "/p:BlockOnPossibleDataLoss=False" : "/p:BlockOnPossibleDataLoss=True";

            var diffProcess = Process.Start($"{SqlPackageUtililtyPath}", $"/a:Script /sf:" +
                $"{Path.Combine(TempFolderBasePath, AppName + "_Staging.dacpac")} /tf:{Path.Combine(TempFolderBasePath, AppName + ".dacpac")}" +
                $" /tdn:{prodDatabaseName} /op:{migrationPath} {ExitIfDataLoss}");

            diffProcess.WaitForExit();

            //Apply migraiton sql
            var filteredMigrationsPath = FilterSqlFileForSchema(migrationPath, AppName);

            var migrationProcess = new Process();
            migrationProcess.StartInfo.RedirectStandardOutput = true;
            migrationProcess.StartInfo.FileName = SqlCommandUtililtyPath;
            migrationProcess.StartInfo.Arguments = $"-S {prodConnection.ConnectionString.Split(';', '=').ElementAt(1)} -i \"{filteredMigrationsPath}\"";
            migrationProcess.Start();

            //Check if data loss exception happens
            var output = migrationProcess.StandardOutput.ReadToEnd();
            if (output.Contains("Msg 50000"))
                throw new FormatException("Data Loss possible. Run again with Force flag to ignore");

            migrationProcess.WaitForExit();
        }

        private string FilterSqlFileForSchema(string filePath, string schemaFilterName)
        {
            var sqlText = File.ReadAllText(filePath);
            //var prefix = sqlText.Substring(0, sqlText.IndexOf("USE [$(DatabaseName)];") + "USE [$(DatabaseName)];".Length);

            //var commands = sqlText.Split(new string[1] { "Go" }, StringSplitOptions.RemoveEmptyEntries);
            //commands = commands.Where(c => c.Contains(schemaFilterName)).ToArray();
            //var rejoined = string.Join("Go", commands);
            var newPath = filePath.Replace(".sql", "_filtered.sql");

            File.WriteAllText(newPath, sqlText);

            return newPath;
        }

        private const string dbNameQualifier = "Database=";
        private string ExtractDatabaseNameFromConnectionString(string connStr)
        {
            var locationOfDb = connStr.IndexOf(dbNameQualifier) + dbNameQualifier.Length;
            var str = connStr.Substring(locationOfDb, connStr.IndexOf(';', locationOfDb) - locationOfDb);
            return str;
        }
    }
}
