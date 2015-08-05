using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;

namespace WebLog2SQL
{
    public static class Program
    {
        internal static readonly Options Settings = new Options();
        private static ConcurrentQueue<FileInfo> _q;
        private static void Consume() { FileInfo file; while (_q.TryDequeue(out file)) File.From(file).Import(); }

        static Program() { Parser.Default.ParseArguments(new string[] { }, Settings); }
        public static void Main(string[] arguments)
        {
            Parser.Default.ParseArgumentsStrict(arguments, Settings);
            try
            {
                if (Settings.Clean)
                {
                    Console.WriteLine("Removing old data...");
                    using (var ctx = new WebLogDB())
                    {
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
                                        "PRIMARY KEY NONCLUSTERED ([Id])" +
                                        "FOREIGN KEY ([FileId]) REFERENCES [dbo].[Files] ([Id]) ON DELETE CASCADE);" +
                                    "CREATE CLUSTERED INDEX [IX_EventOrder] " +
                                        "ON [dbo].[Events]([FileID] ASC, [date] ASC, [time] ASC);" +
                                "END " +
                            "DELETE Files WHERE Updated < {0}", Settings.MaxAge);
                    }
                }

                Console.WriteLine("Locating files...");
                var files = new List<FileInfo>();
                foreach (var dir in Settings.Include)
                    if (Directory.Exists(dir))
                        new DirectoryInfo(dir).Search(files);
                _q = new ConcurrentQueue<FileInfo>(
                    files.Distinct()
                         .Where(f => f.LastWriteTime > Settings.MaxAge)
                         .OrderBy(f => f.LastWriteTime)
                );

                Console.WriteLine("Processing files...");
                Parallel.Invoke(Enumerable.Repeat((Action)Consume, Environment.ProcessorCount).ToArray());
            }
            catch (Exception ex) { Debug.WriteLine(ex); throw; }
        }

        private static void Search(this DirectoryInfo dir, List<FileInfo> files)
        {
            if (Settings.Exclude != null && Settings.Exclude.Intersect(new[] { dir.FullName, dir.Name }).Any()) return;
            foreach (var sub in dir.GetDirectories())
                sub.Search(files);
            files.AddRange(dir.GetFiles(Settings.Filter, SearchOption.TopDirectoryOnly));
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
