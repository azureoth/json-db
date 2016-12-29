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
        private string DatabaseName;
        private SqlConnection connection;
        private SchemaTranslator translator;
        private string TempFolderBasePath;
        private string SqlPackageUtililtyPath;
        private string SqlCommandUtililtyPath;

        internal SchemaBuilder(string databaseName, string databaseConnectionString)
        {
            DatabaseName = databaseName;
            connection = new SqlConnection(databaseConnectionString);
            translator = new SchemaTranslator();
            TempFolderBasePath = "C:/Logs/";
            SqlPackageUtililtyPath = "\"C:\\Program Files (x86)\\Microsoft SQL Server\\130\\DAC\\bin\\SqlPackage.exe\"";
            SqlCommandUtililtyPath = "\"C:\\Program Files\\Microsoft SQL Server\\110\\Tools\\Binn\\SQLCMD.EXE\"";
        }


        public async Task CreateSchema(Dictionary<string, JsonTable> NewSchema, string AppName)
        {
            await connection.OpenAsync();

            var sqlSchema = translator.JsonToSQL(NewSchema, DatabaseName, AppName, false);

            //Apply new staging schema
            var createSchemaCommand = new SqlCommand(sqlSchema.SchemaCreateSQL);
            createSchemaCommand.Connection = connection;
            await createSchemaCommand.ExecuteNonQueryAsync();

            //Create new tables for staging schema
            var applyTempSchemaCommand = new SqlCommand(sqlSchema.RawSQL);
            applyTempSchemaCommand.Connection = connection;
            await applyTempSchemaCommand.ExecuteNonQueryAsync();

            connection.Close();
        }

        public async Task UpdateSchema(Dictionary<string, JsonTable> OldSchema, Dictionary<string, JsonTable> NewSchema, string AppName)
        {
            await connection.OpenAsync();

            var stagingSchemaName = AppName + "_Stage";

            //Generate new staging schema
            var newGeneratedSchema = translator.JsonToSQL(NewSchema, DatabaseName, stagingSchemaName, false);

            //Drop existing staging schema
            var dropExistingStageSchemaCommand = new SqlCommand($"exec CleanUpSchema '{stagingSchemaName}', 'w'");
            dropExistingStageSchemaCommand.Connection = connection;
            await dropExistingStageSchemaCommand.ExecuteNonQueryAsync();

            //Apply new staging schema
            var createSchemaCommand = new SqlCommand(newGeneratedSchema.SchemaCreateSQL);
            createSchemaCommand.Connection = connection;
            await createSchemaCommand.ExecuteNonQueryAsync();

            //Create new tables for staging schema
            var applyTempSchemaCommand = new SqlCommand(newGeneratedSchema.RawSQL);
            applyTempSchemaCommand.Connection = connection;
            await applyTempSchemaCommand.ExecuteNonQueryAsync();

            //Package staging schema for comparison
            var stagingPackages = NewSchema.Keys.Select(k => $"/C {SqlPackageUtililtyPath} /a:Extract /scs:{connection.ConnectionString} /tf:{Path.Combine(TempFolderBasePath, stagingSchemaName + ".dacpac")} /p:TableData={stagingSchemaName}.*");
            var processes = stagingPackages.Select(p => Process.Start("CMD.exe", p)).ToList();

            //Package prod schema for comparison
            var prodPackages = OldSchema.Keys.Select(k => $"/C {SqlPackageUtililtyPath} /a:Extract /scs:{connection.ConnectionString} /tf:{Path.Combine(TempFolderBasePath, AppName + ".dacpac")} /p:TableData={AppName}.{k}");
            processes.AddRange(stagingPackages.Select(p => Process.Start("CMD.exe", p)));

            foreach(var process in processes)
            {
                process.WaitForExit();
            }

            //Generate migration sql
            var migrationPath = Path.Combine(TempFolderBasePath, AppName + "M.sql");

            string diffPackage = $"/C {SqlPackageUtililtyPath} /a:Script /sf:{Path.Combine(TempFolderBasePath, stagingSchemaName + ".dacpac")} /tf:{Path.Combine(TempFolderBasePath, AppName + ".dacpac")} /tdn:{DatabaseName} /op:{migrationPath}";
            var diffProcess = Process.Start("CMD.exe", diffPackage);
            diffProcess.WaitForExit();

            //Apply migraiton sql
            var migrationCommand = new SqlCommand(ReadSqlFile(migrationPath));
            migrationCommand.Connection = connection;
            await migrationCommand.ExecuteNonQueryAsync();

            connection.Close();
        }

        private string ReadSqlFile(string filePath)
        {
            return File.ReadAllText(filePath);
        }   
    }
}
