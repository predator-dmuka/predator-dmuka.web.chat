using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        static string __sourceCodePath = "";
        static string __bashProcessesPath = "";
        #endregion

        #region Constructors
        static Helper()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            __bashProcessesPath = Path.Combine(currentDirectory, "bash-processes.txt");

            if (
                File.Exists(__bashProcessesPath) == false || 
                (DateTime.UtcNow.AddMilliseconds(-1 * Environment.TickCount) - new DateTime(Convert.ToInt64(File.ReadAllText(__bashProcessesPath).Split(new char[] { '~' }, StringSplitOptions.RemoveEmptyEntries)[0]))).TotalSeconds > 1
                )
                File.WriteAllText(__bashProcessesPath, DateTime.UtcNow.AddMilliseconds(-1 * Environment.TickCount).Ticks.ToString());

            string parent = Directory.GetParent(currentDirectory).FullName;
            __sourceCodePath = Path.Combine(parent, "source-code");
            __configPath = Path.Combine(__sourceCodePath, "config.json");
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

        public static bool TryCatch(Action action)
        {
            try
            {
                action();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("We couldn't. Do you want see the error? (y/n) ");
                if (Console.ReadLine().ToLower() == "y")
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("Enter a line to continue checking database...");
                    Console.ReadLine();
                }

                return false;
            }
        }

        public static string GetBashProcessId(string name)
        {
            var bashProcessesList = File.ReadAllText(__bashProcessesPath).Split(new char[] { '~' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            for (int i = 1; i < bashProcessesList.Count; i++)
            {
                var bashProcess = bashProcessesList[i];

                string bashProcessName = bashProcess.Split('/')[0];

                if (bashProcessName == name)
                    return bashProcess.Split('/')[1];
            }

            return "";
        }

        public static void SetBashProcessId(string name, string processId)
        {
            var bashProcessesList = File.ReadAllText(__bashProcessesPath).Split(new char[] { '~' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var newRow = name + "/" + processId;

            var exists = false;
            for (int i = 1; i < bashProcessesList.Count; i++)
            {
                var bashProcess = bashProcessesList[i];

                string bashProcessName = bashProcess.Split('/')[0];

                if (bashProcessName == name)
                {
                    exists = true;
                    bashProcessesList[i] = newRow;
                    break;
                }
            }

            if (exists == false)
                bashProcessesList.Add(newRow);

            File.WriteAllText(__bashProcessesPath, string.Join("~", bashProcessesList));
        }


        public static string Md5(string metin)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] btr = Encoding.UTF8.GetBytes(metin);
            btr = md5.ComputeHash(btr);
            StringBuilder sb = new StringBuilder();

            foreach (byte ba in btr)
            {
                sb.Append(ba.ToString("x2").ToLower());
            }

            return sb.ToString();
        }

        public static void RunBashCommand(string commandName, string name, string arguments, string workingDirectory, bool main, bool killPrevious)
        {
            commandName = commandName.Replace("~", "-").Replace("/", "-");

            var cmd = (name + " " + arguments) + (main == false ? " &" : "");

            if (main == false)
                cmd += Environment.NewLine + "echo \"-*=*-\"$!\"-*=*-\"";

            if (killPrevious == true)
            {
                var processId = GetBashProcessId(commandName);
                if (processId != "")
                {

                    cmd = "kill " + processId + Environment.NewLine + cmd;
                }
            }

            Console.WriteLine();
            Console.WriteLine("****************************************");
            Console.WriteLine();
            Console.WriteLine("Process Name = " + name);
            Console.WriteLine("CMD = " + cmd);
            Console.WriteLine();
            Console.WriteLine("****************************************");
            Console.WriteLine();

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    WorkingDirectory = Path.Combine(__sourceCodePath, workingDirectory),
                    Arguments = "-c \"" + cmd.Replace("\"", "\\\"") + "\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    ErrorDialog = false
                }
            };

            string newProcessId = "";
            process.OutputDataReceived += (a, b) =>
            {
                if (b.Data != null && newProcessId == "" && b.Data.Contains("-*=*-"))
                {
                    var split = b.Data.Split(new string[] { "-*=*-" }, StringSplitOptions.None);
                    newProcessId = split[1];
                }
                Console.WriteLine(b.Data);
            };
            process.ErrorDataReceived += (a, b) => Console.WriteLine(b.Data);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            if (main)
                process.WaitForExit();

            while (main == false && newProcessId == "")
                Thread.Sleep(1);

            if (main == false)
                SetBashProcessId(commandName, newProcessId);
        }

        public static void KillBashProcess(string commandName, string workingDirectory)
        {
            commandName = commandName.Replace("~", "-").Replace("/", "-");

            string cmd = "";
            var processId = GetBashProcessId(commandName);
            if (processId != "")
                cmd = "kill " + processId + Environment.NewLine + cmd;

            Console.WriteLine();
            Console.WriteLine("****************************************");
            Console.WriteLine();
            Console.WriteLine("CMD = " + cmd);
            Console.WriteLine();
            Console.WriteLine("****************************************");
            Console.WriteLine();

            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    WorkingDirectory = Path.Combine(__sourceCodePath, workingDirectory),
                    Arguments = "-c \"" + cmd.Replace("\"", "\\\"") + "\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    ErrorDialog = false
                }
            };

            process.OutputDataReceived += (a, b) => Console.WriteLine(b.Data);
            process.ErrorDataReceived += (a, b) => Console.WriteLine(b.Data);
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();

            SetBashProcessId(commandName, "");
        }

        #endregion
    }
}
