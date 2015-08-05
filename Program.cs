using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using FileHook = System.Tuple<WebLog2SQL.Location, System.IO.FileInfo>;

namespace WebLog2SQL
{
    public static class Program
    {
        internal static readonly Options Settings = new Options();
        private static ConcurrentQueue<FileHook> _q;
        private static void Consume() { FileHook file; while (_q.TryDequeue(out file)) File.From(file).Import(); }

        static Program() { Parser.Default.ParseArguments(new string[] { }, Settings); }
        public static void Main(string[] arguments)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Console.WriteLine(e);
            Parser.Default.ParseArgumentsStrict(arguments, Settings);
            using (var ctx = new WebLogDB())
            {
                Console.WriteLine("Checking the database...");
                ctx.Database.CommandTimeout = 600;
                ctx.Database.ExecuteSqlCommand(
                    "IF OBJECT_ID('dbo.Events') IS NULL " +
                        "BEGIN " +
                            "CREATE TABLE [dbo].[Events] (" +
                                "[Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID()," +
                                "[FileId] BIGINT NOT NULL," +
                                "[datetime] AS dateadd(day,datediff(day,'19000101',[date]),CONVERT([datetimeoffset](7),[time],(0)))," +
                                "[date] DATE NOT NULL," +
                                "[time] TIME(7) NOT NULL," +
                                "[cs-bytes] INT NULL," +
                                "[s-port] INT NULL," +
                                "[sc-status] INT NULL," +
                                "[sc-substatus] INT NULL," +
                                "[sc-win32-status] BIGINT NULL," +
                                "[sc-bytes] INT NULL," +
                                "[time-taken] INT NULL " +
                                "CONSTRAINT [PK_Events] PRIMARY KEY NONCLUSTERED ([Id])" +
                                "CONSTRAINT [FK_Events_Files_FileId] FOREIGN KEY ([FileId])" +
                                    "REFERENCES [dbo].[Files] ([Id]) ON DELETE CASCADE);" +
                            "CREATE CLUSTERED INDEX [IX_Events_Order] " +
                                "ON [dbo].[Events]([FileID] ASC, [date] ASC, [time] ASC);" +
                            "CREATE NONCLUSTERED INDEX [IX_Events_FileId] " +
                                "ON [dbo].[Events]([FileId]);" +
                        "END");
                if (!Settings.Keep)
                {
                    Console.WriteLine("Removing old data...");
                    ctx.Database.ExecuteSqlCommand("DELETE Files FROM Files JOIN Locations ON LocationName = Locations.Name AND Updated < GETDATE()-DaysToKeep");
                }

                Console.WriteLine("Locating files...");
                _q = new ConcurrentQueue<FileHook>(
                    ctx.Locations.AsEnumerable()
                       .SelectMany(l => l.Search())
                       .OrderBy(f => f.Item2.CreationTime)
                       .ToArray());
            }

            Console.WriteLine("Processing files...");
            Parallel.Invoke(Enumerable.Repeat((Action)Consume, Environment.ProcessorCount).ToArray());
        }

        internal static object GetField(this object obj, string name)
        {
            return obj.GetType().InvokeMember(name,
                BindingFlags.DeclaredOnly |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.GetField,
                null, obj, null);
        }
    }
}
