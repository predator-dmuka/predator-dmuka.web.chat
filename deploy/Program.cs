using Newtonsoft.Json.Linq;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;

namespace deploy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("====Welcome predator-dmuka.web.chat Deploy====");
            Console.WriteLine("First, you have to make a choice you want.");
            Console.WriteLine("Let's begin.");
            Console.WriteLine("---------------------------------------------------");

            while (true)
            {
                Console.WriteLine("---------------------------------------------------");

                Console.WriteLine("00\t\t= Exit Deploy");
                Console.WriteLine("DB-001\t\t= Check Databases by 'predator-dmuka.web.chat/source-code/config.js'");
                Console.WriteLine("DB-002\t\t= Update Tables with 'predator-dmuka.web.chat/database/<db_name>__migrations' on Databases");
                Console.WriteLine("DB-003\t\t= Remove All Table on Database (We will ask for each)");

                Console.Write("Write Code = ");
                string code = Console.ReadLine();
                if (code == "00")
                    break;

                Console.WriteLine("===================================================");
                Console.WriteLine("Begin of " + code);
                switch (code)
                {
                    case "DB-001":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                            {
                                Console.WriteLine("We are trying to connect to server...");
                                var connectionStringServer = property.Value["connection_string_server"].Value<string>();
                                try
                                {
                                    using (NpgsqlConnection connection = new NpgsqlConnection(connectionStringServer))
                                    {
                                        connection.Open();
                                        connection.Close();
                                    }

                                    Console.WriteLine("We connected to server successfully.", property.Key);
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("We couldn't. Do you want see the error? (y/n) ");
                                    if (Console.ReadLine().ToLower() == "y")
                                    {
                                        Console.WriteLine(ex.ToString());
                                        Console.WriteLine("Enter a line to continue checking database...");
                                        Console.ReadLine();
                                    }
                                    continue;
                                }

                                Console.WriteLine("We are trying to connect to '{0}' database...", property.Key);
                                var connectionString = property.Value["connection_string"].Value<string>();
                                try
                                {
                                    using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
                                    {
                                        connection.Open();
                                        connection.Close();
                                    }

                                    Console.WriteLine("We connected to '{0}' database successfully.", property.Key);
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("We couldn't. Do you want see the error? (y/n) ");
                                    if (Console.ReadLine().ToLower() == "y")
                                    {
                                        Console.WriteLine(ex.ToString());
                                        Console.WriteLine("Enter a line to continue checking database...");
                                        Console.ReadLine();
                                    }

                                }
                            }
                        }
                        break;
                    case "DB-002":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                            {
                                Console.WriteLine("We are trying to connect to '{0}' database...", property.Key);
                                var connectionString = property.Value["connection_string"].Value<string>();
                                try
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
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("We couldn't. Do you want see the error? (y/n) ");
                                    if (Console.ReadLine().ToLower() == "y")
                                    {
                                        Console.WriteLine(ex.ToString());
                                        Console.WriteLine("Enter a line to continue checking database...");
                                        Console.ReadLine();
                                    }

                                }
                            }
                        }
                        break;
                    case "DB-003":
                        {
                            var databases = (JObject)Helper.GetConfigAsJToken("databases");

                            foreach (var property in databases)
                            {
                                Console.Write("Do you want to remove all table on {0} database? (y/n) ", property.Key);
                                if (Console.ReadLine().ToLower() != "y")
                                    continue;

                                Console.WriteLine("We are trying to connect to server...", property.Key);
                                var connectionStringServer = property.Value["connection_string_server"].Value<string>();
                                try
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
                                }
                                catch (Exception ex)
                                {
                                    Console.Write("We couldn't. Do you want see the error? (y/n) ");
                                    if (Console.ReadLine().ToLower() == "y")
                                    {
                                        Console.WriteLine(ex.ToString());
                                        Console.WriteLine("Enter a line to continue checking database...");
                                        Console.ReadLine();
                                    }

                                }
                            }
                        }
                        break;
                    default:
                        Console.WriteLine("Wrong code. Please write a code which is in the list. For example, '00' is for exit.");
                        break;
                }

                Console.WriteLine("End of " + code);
            }

            Console.WriteLine("We hope that you don't come here again. Type a line to exit...");
            Console.ReadLine();
        }
    }
}
