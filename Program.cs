using System;
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

        static Program()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.Fatal(e.ExceptionObject as Exception, "Unhandled Exception");
            Application.ThreadException += (s, e) => Logger.Error(e.Exception, "Unhandled Thread Exception");
        }
        public static void Main(string[] arguments)
        {
            try
            {
                Location[] locations;
                Logger.Warn(Environment.CommandLine);
                Parser.Default.ParseArguments(arguments, Settings);
                using (var ctx = new WebLogDB())
                {
                    locations = ctx.Locations.ToArray();
                    if (!Settings.Keep)
                    {
                        const string sql = "SELECT Files.* FROM Files " +
                                           "JOIN Locations ON LocationName = Locations.Name " +
                                           "AND DATEDIFF(d, Updated, GETDATE()) > DaysToKeep " +
                                           "ORDER BY Files.Id";
                        var originalTimeout = ctx.Database.CommandTimeout;
                        ctx.Database.CommandTimeout = 300;
                        foreach (var file in ctx.Files.SqlQuery(sql).ToArray())
                        {
                            Logger.Info("Removing {0} (includes {1} events)", file.FullName, file.EventCount);
                            ctx.Files.Remove(file);
                            ctx.SaveChanges();
                        }
                        ctx.Database.CommandTimeout = originalTimeout;
                    }
                }

                File.Events.MaxRowsToBuffer = Settings.BufferSize;
                Logger.Warn("Processing files");
                Task.WaitAll((
                    from loc in locations.AsEnumerable()
                    from file in loc.GetFiles()
                    orderby file.CreationTime
                    select File.From(loc, file).ImportAsync()
                    ).ToArray());
            }
            catch (Exception ex) { Logger.Fatal(ex, "Fatal Error"); }
            finally { Logger.Warn("Done."); }
        }

        internal static object GetField(this object obj, string name)
            => obj.GetType().InvokeMember(name, (BindingFlags)0x0436, null, obj, null);
    }
}
