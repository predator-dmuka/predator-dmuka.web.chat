using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace deploy
{
    public static class Helper
    {
        #region Variables
        static string __configPath = "";
        static string __databasePath = "";
        #endregion

        #region Constructors
        static Helper()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string parent = Directory.GetParent(currentDirectory).FullName;
            __configPath = Path.Combine(parent, "source-code", "config.json");
            __databasePath = Path.Combine(parent, "database");
        }
        #endregion

        #region Methods
        public static JToken GetConfigAsJToken(string path)
        {
            JToken config = JsonConvert.DeserializeObject<JToken>(File.ReadAllText(__configPath));
            return config.SelectToken(path);
        }

        public static string GetConfig(string path)
        {
            return GetConfigAsJToken(path).ToString();
        }

        public static void CreateMigrationTable(NpgsqlConnection connection)
        {
            string migrationTableSql = @"
DO
$do$
begin
	IF EXISTS (
	   SELECT 1 
	   FROM   pg_catalog.pg_class c
	   JOIN   pg_catalog.pg_namespace n ON n.oid = c.relnamespace
	   WHERE  n.nspname = 'public'
	   AND    c.relname = '__migrations'
	   AND    c.relkind = 'r'    -- only tables
	   ) = false THEN
	   		CREATE TABLE public.__migrations
			(
				migration_id bigserial NOT NULL,
				file_name character varying(1000) NOT NULL,
				PRIMARY KEY (migration_id)
			)
			WITH (
				OIDS = FALSE
			);

			ALTER TABLE public.__migrations
				OWNER to postgres;
			COMMENT ON TABLE public.__migrations
				IS 'This table store migrations to submit last database changes.';
	END IF;
end
$do$
";
            using (NpgsqlCommand migrationCommand = new NpgsqlCommand(migrationTableSql, connection))
            {
                migrationCommand.ExecuteNonQuery();
            }
        }

        public static void CheckMigrationFiles(NpgsqlConnection connection, string databaseName, Action<string, string> runMigration)
        {
            List<string> migrationsDb = new List<string>();

            using (NpgsqlCommand migrationReadCommand = new NpgsqlCommand("SELECT file_name FROM public.__migrations;", connection))
            using (NpgsqlDataReader migrationReadReader = migrationReadCommand.ExecuteReader())
                while (migrationReadReader.Read())
                    migrationsDb.Add(migrationReadReader[0].ToString());

            var migrationDatabaseDirectoryName = databaseName + "__migrations";
            var migrationFiles = Directory.GetFiles(Path.Combine(__databasePath, migrationDatabaseDirectoryName))
                            .Select(o => Path.GetFileName(o))
                            .Where(o => migrationsDb.Any(a => a == o) == false)
                            .OrderBy(o => Path.GetFileNameWithoutExtension(o)).ToArray();

            foreach (var fileName in migrationFiles)
            {
                string migrationSql = File.ReadAllText(Path.Combine(__databasePath, migrationDatabaseDirectoryName, fileName));
                runMigration(fileName, migrationSql);

                using (NpgsqlCommand addMigrationCommand = new NpgsqlCommand("INSERT INTO public.__migrations(file_name) VALUES(@file_name);", connection))
                {
                    addMigrationCommand.Parameters.Add(new NpgsqlParameter("@file_name", fileName));
                    addMigrationCommand.ExecuteNonQuery();
                }
            }
        }
        #endregion
    }
}
