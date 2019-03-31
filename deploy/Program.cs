using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace deploy
{
    class Program
    {
        static void Main(string[] args)
        {
            Helper.Md5("test");

            Console.WriteLine("====Welcome predator-dmuka.web.chat Deploy====");
            Console.WriteLine("First, you have to make a choice you want.");
            Console.WriteLine("Let's begin.");
            Console.WriteLine("---------------------------------------------------");

            while (true)
            {
                Console.WriteLine("---------------------------------------------------");

                Console.WriteLine("Write Code (help) = ");
                string code = Console.ReadLine();
                if (code == "00")
                    break;

                Console.WriteLine("===================================================");
                Console.WriteLine("Begin of " + code);
                Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
                Console.WriteLine();
                switch (code)
                {
                    case "help":
                        {
                            Console.WriteLine("00                                    = Exit Deploy");
                            Console.WriteLine("db--check-databases                   = Check Databases by 'predator-dmuka.web.chat/source-code/config.js'");
                            Console.WriteLine("db--update-tables                     = Update Tables with 'predator-dmuka.web.chat/database/<db_name>__migrations' on Databases");
                            Console.WriteLine("db--remove-all-tables-on-all-database = Remove All Tables on Database");
                            Console.WriteLine("db--remove-all-tables                 = Remove All Tables on Database You Want");
                            Console.WriteLine("pr--show-all-projects-status          = Show All Projects Status");
                            Console.WriteLine("pr--restart-all-projects              = Restart All Projects");
                            Console.WriteLine("pr--restart-project                   = Restart Project You Want");
                            Console.WriteLine("pr--kill-all-projects                 = Kill All Projects");
                            Console.WriteLine("pr--kill-project                      = Kill Project You Want");
                        }
                        break;
                    case "db--check-databases":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                            {
                                Console.WriteLine("We are trying to connect to server...");
                                var connectionStringServer = property.Value["connection_string_server"].Value<string>();
                                if (Helper.TryCatch(
                                    () =>
                                    {
                                        using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringServer))
                                        {
                                            connection.Open();
                                            connection.Close();
                                        }

                                        Console.WriteLine("We connected to server successfully.", property.Key);
                                    }) == false)
                                    continue;

                                Console.WriteLine("We are trying to connect to '{0}' database...", property.Key);
                                var connectionString = property.Value["connection_string"].Value<string>();

                                Helper.TryCatch(() =>
                                {
                                    using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                                    {
                                        connection.Open();
                                        connection.Close();
                                    }

                                    Console.WriteLine("We connected to '{0}' database successfully.", property.Key);
                                });
                            }
                        }
                        break;
                    case "db--update-tables":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                            {
                                Console.WriteLine("We are trying to connect to '{0}' database...", property.Key);
                                var connectionString = property.Value["connection_string"].Value<string>();
                                Helper.TryCatch(() =>
                                {
                                    using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                                    {
                                        connection.Open();

                                        Console.WriteLine("Creating migration table, if it's not exist...");
                                        Helper.CreateMigrationTable(connection);
                                        Console.WriteLine("Migration table checking completed.");

                                        Helper.CheckMigrationFiles(connection, property.Key, (fileName, migrationSql) =>
                                        {
                                            Console.WriteLine("{0} migration is applying...", fileName);

                                            using (NpgsqlCommand migrationCommand = new NpgsqlCommand(migrationSql, connection))
                                                migrationCommand.ExecuteNonQuery();

                                            Console.WriteLine("{0} migration applied...", fileName);
                                        });

                                        connection.Close();
                                    }

                                    Console.WriteLine("We updated to '{0}' database successfully.", property.Key);
                                });
                            }
                        }
                        break;
                    case "db--remove-all-tables-all-database":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                                removeAllDatasFromDatabase(property);
                        }
                        break;
                    case "db--remove-all-tables":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            Console.WriteLine("Write you db name = ");
                            var dbName = Console.ReadLine();

                            Helper.TryCatch(() =>
                            {
                                removeAllDatasFromDatabase(new KeyValuePair<string, JToken>(dbName, databases[dbName]));
                            });
                        }
                        break;
                    case "pr--show-all-projects-status":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");
                            foreach (var project in projects)
                            {
                                bool open = false;
                                try
                                {
                                    open = Process.GetProcessById(Convert.ToInt32(Helper.GetBashProcessId(project.Key))).HasExited == false;
                                }
                                catch { }

                                Console.WriteLine("\"{0}\"\t\t\t= \"{1}\"", project.Key, open ? "OPENED" : "CLOSED");
                            }
                        }
                        break;
                    case "pr--restart-all-projects":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");

                            foreach (var project in projects)
                                restartProject(project);
                        }
                        break;
                    case "pr--restart-project":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");

                            Console.WriteLine("Write you project name = ");
                            var projectName = Console.ReadLine();

                            restartProject(new KeyValuePair<string, JToken>(projectName, projects[projectName]));
                        }
                        break;
                    case "pr--kill-all-projects":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");
                            foreach (var project in projects)
                                killProject(project);
                        }
                        break;
                    case "pr--kill-project":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");

                            Console.WriteLine("Write you project name = ");
                            var projectName = Console.ReadLine();

                            killProject(new KeyValuePair<string, JToken>(projectName, projects[projectName]));
                        }
                        break;
                    default:
                        Console.WriteLine("Wrong code. Please write a code which is in the list. For example, '00' is for exit.");
                        break;
                }

                Console.WriteLine();
                Console.WriteLine("<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                Console.WriteLine("End of " + code);
            }

            Console.WriteLine("We hope that you don't come here again. Type a line to exit...");
            Console.ReadLine();
        }

        private static void killProject(KeyValuePair<string, JToken> project)
        {
            Console.WriteLine("{0} project's commands is being killed...", project.Key);

            var commands = (JArray)project.Value["commands"];
            foreach (var command in commands)
            {
                var commandIsMain = command["main"].Value<bool>();

                var commandName = command["name"].Value<string>();
                if (commandIsMain == true)
                    Helper.KillBashProcess(project.Key, command["path"].Value<string>());
            }

            Console.WriteLine("{0} project's commands completed.", project.Key);
        }

        private static void restartProject(KeyValuePair<string, JToken> project)
        {
            Console.WriteLine("{0} project's commands is running...", project.Key);

            var commands = (JArray)project.Value["commands"];
            foreach (var command in commands)
            {
                var commandIsMain = command["main"].Value<bool>();

                var commandName = command["name"].Value<string>();

                Helper.RunBashCommand(project.Key, commandName, command["arguments"].Value<string>(), command["path"].Value<string>(), !commandIsMain, commandIsMain);
            }

            Console.WriteLine("{0} project's commands completed.", project.Key);
        }

        private static void removeAllDatasFromDatabase(KeyValuePair<string, JToken> property)
        {
            Console.WriteLine("We are trying to connect to server...", property.Key);
            var connectionStringServer = property.Value["connection_string_server"].Value<string>();
            Helper.TryCatch(() =>
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringServer))
                {
                    connection.Open();

                    string clearTablesSql = @"
DO $$
DECLARE 
    brow record;
BEGIN
    FOR brow IN (select 'drop table ""' || tablename || '"" cascade;' as table_name from pg_tables where schemaname <> 'pg_catalog' AND schemaname <> 'information_schema') LOOP
        EXECUTE brow.table_name;
    END LOOP;
END; $$
";
                    using (NpgsqlCommand clearTablesCommand = new NpgsqlCommand(clearTablesSql, connection))
                        clearTablesCommand.ExecuteNonQuery();

                    connection.Close();
                }

                Console.WriteLine("We removed all tables on '{0}' database successfully.", property.Key);
            });
        }
    }
}
