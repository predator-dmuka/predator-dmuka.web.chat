using Newtonsoft.Json;
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
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "--background":
                        {
                            Console.WriteLine(args[1]);
                            var processJson = JsonConvert.DeserializeObject<JToken>(args[1]);
                            Helper.RunBashCommand(
                                processJson["commandName"].Value<string>(),
                                processJson["name"].Value<string>(),
                                processJson["arguments"].Value<string>(),
                                processJson["workingDirectory"].Value<string>(),
                                true,
                                true);
                            return;
                        }
                    default:
                        break;
                }
            }

            Helper.Md5("test");

            Console.WriteLine("====Welcome predator-dmuka.web.chat Deploy====");
            Console.WriteLine("First, you have to make a choice you want.");
            Console.WriteLine("If you don't know any command, you should use 'help'.");
            Console.WriteLine("Let's begin.");
            Console.WriteLine("---------------------------------------------------");

            while (true)
            {
                Console.WriteLine("---------------------------------------------------");

                Console.WriteLine("Write Code = ");
                string code = Console.ReadLine();
                if (code == "exit")
                    break;

                Console.WriteLine("===================================================");
                Console.WriteLine("Begin of " + code);
                Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
                Console.WriteLine();
                switch (code)
                {
                    case "help":
                        {
                            Console.WriteLine("exit    = Exit Deploy");
                            Console.WriteLine("db -c   = Check Databases by 'predator-dmuka.web.chat/source-code/config.js'");
                            Console.WriteLine("db -u   = Update Tables with 'predator-dmuka.web.chat/database/<db_name>__migrations' on Databases");
                            Console.WriteLine("db -rta = Remove All Tables on All Database");
                            Console.WriteLine("db -rt  = Remove All Tables on Database You Want");
                            Console.WriteLine("pr -s   = Show All Projects Status");
                            Console.WriteLine("pr -ra  = Restart All Projects");
                            Console.WriteLine("pr -r   = Restart Project You Want");
                            Console.WriteLine("pr -ka  = Kill All Projects");
                            Console.WriteLine("pr -k   = Kill Project You Want");
                        }
                        break;
                    case "db -c":
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
                    case "db -u":
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
                    case "db -rta":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                                removeAllDatasFromDatabase(property);
                        }
                        break;
                    case "db -rt":
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
                    case "pr -s":
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
                    case "pr -ra":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");

                            foreach (var project in projects)
                                restartProject(project);
                        }
                        break;
                    case "pr -r":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");

                            Console.WriteLine("Write you project name = ");
                            var projectName = Console.ReadLine();

                            restartProject(new KeyValuePair<string, JToken>(projectName, projects[projectName]));
                        }
                        break;
                    case "pr -ka":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");
                            foreach (var project in projects)
                                killProject(project);
                        }
                        break;
                    case "pr -k":
                        {
                            var projects = (JObject)Helper.GetConfigAsJToken("projects");

                            Console.WriteLine("Write you project name = ");
                            var projectName = Console.ReadLine();

                            killProject(new KeyValuePair<string, JToken>(projectName, projects[projectName]));
                        }
                        break;
                    default:
                        Console.WriteLine("Wrong code. Please write a code which is in the list. For example, 'exit' is for exit.");
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

                Helper.RunBashCommand(project.Key, commandName, command["arguments"].Value<string>(), command["path"].Value<string>(), commandIsMain, false);
            }

            Console.WriteLine("{0} project's commands completed.", project.Key);
        }

        private static void removeAllDatasFromDatabase(KeyValuePair<string, JToken> property)
        {
            Console.WriteLine("We are trying to connect to server...", property.Key);
            var connectionString = property.Value["connection_string"].Value<string>();
            Helper.TryCatch(() =>
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string clearTablesSql = @"
DO $$
DECLARE 
    brow record;
BEGIN
    FOR brow IN (select 'drop table ""' || schemaname || '"".""' || tablename || '"" cascade;' as table_name from pg_tables where schemaname <> 'pg_catalog' AND schemaname <> 'information_schema') LOOP
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
