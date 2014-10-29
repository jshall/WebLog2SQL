using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;

namespace WebLog2SQL
{
    public static class Program
    {
        internal static Options Settings = new Options();
        public static void Main(string[] args)
        {
            Parser.Default.ParseArgumentsStrict(args, Settings);
            try
            {
                if (Settings.Clean)
                {
                    Console.WriteLine("Removing old data...");
                    using (var ctx = new WebLogDB { CommandTimeout = 600 })
                        ctx.ExecuteCommand("delete [File] where [Updated] < {0}", Settings.MaxAge);
                }

                Console.WriteLine("Locating files...");
                var files = new List<FileInfo>();
                foreach (var dir in Settings.Include)
                    if (Directory.Exists(dir))
                        new DirectoryInfo(dir).Search(files);
                var q = new ConcurrentQueue<FileInfo>(
                    files.Distinct()
                         .Where(f => f.LastWriteTime > Settings.MaxAge)
                         .OrderByDescending(f => f.LastWriteTime)
                );

                Console.WriteLine("Processing files...");
                Parallel.Invoke(
                    Enumerable.Repeat<Action>(() =>
                        {
                            FileInfo file;
                            while (q.TryDequeue(out file))
                                File.from(file).Import();
                        }, Environment.ProcessorCount).ToArray());
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }

        private static void Search(this DirectoryInfo dir, List<FileInfo> files)
        {
            if (Settings.Exclude.Intersect(new[] { dir.FullName, dir.Name }).Any()) return;
            foreach (var sub in dir.GetDirectories())
                sub.Search(files);
            files.AddRange(dir.GetFiles(Settings.Filter, SearchOption.TopDirectoryOnly));
        }

        internal static object GetField(this object obj, string name)
        {
            return obj.GetType().InvokeMember(name,
                System.Reflection.BindingFlags.DeclaredOnly |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.GetField,
                null, obj, null);
        }
    }
}
