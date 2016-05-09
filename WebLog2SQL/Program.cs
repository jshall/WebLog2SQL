﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using CommandLine;
using NLog;

namespace WebLog2SQL
{
    public static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        internal static readonly Options Settings = new Options();

        public static void Main(string[] arguments)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.Fatal(e.ExceptionObject as Exception, "Unhandled Exception");
            Application.ThreadException += (s, e) => Logger.Error(e.Exception, "Unhandled Thread Exception");
            try
            {
                Logger.Warn(Environment.CommandLine);
                Parser.Default.ParseArguments(arguments, Settings);
                Import();
            }
            catch (Exception ex) when (!Break()) { Logger.Fatal(ex, "Fatal Error"); }
            finally { Logger.Warn("Done."); }
        }

        public static void Import()
        {
            IEnumerable<Location> locations;
            using (var ctx = new WebLogDB())
            {
                locations = ctx.Locations.ToArray();
                if (!Settings.Keep)
                {
                    var list = from f in ctx.Files.ToArray()
                               where (f.Updated - DateTimeOffset.Now).TotalDays > f.Location.DaysToKeep
                               select f;
                    var originalTimeout = ctx.Database.CommandTimeout;
                    ctx.Database.CommandTimeout = 300;
                    foreach (var file in list)
                    {
                        Logger.Info($"Removing {file.FullName} (includes {file.Events?.Count ?? 0} events)");
                        ctx.Files.Remove(file);
                        ctx.SaveChanges();
                    }
                    ctx.Database.CommandTimeout = originalTimeout;
                }
                foreach (var loc in locations)
                    ((IObjectContextAdapter)ctx).ObjectContext.Detach(loc);
            }

            File.Buffer.MaxRowsToBuffer = Settings.BufferSize;
            Logger.Warn("Processing files");
            Task.WaitAll((
                from loc in locations.AsEnumerable()
                from file in loc.GetFiles()
                orderby file.CreationTime
                select File.ImportAsync(loc, file)
                ).ToArray());
        }

        internal static bool Break()
        {
            if (!Debugger.IsAttached)
                return false;
            Debugger.Break();
            return true;
        }

        internal static object GetField(this object obj, string name)
            => obj.GetType().InvokeMember(name, (BindingFlags)0x0436, null, obj, null);

        internal static bool Matches(this string a, string b)
           => string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);
    }
}
