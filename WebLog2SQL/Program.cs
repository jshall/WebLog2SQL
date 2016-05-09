using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using CommandLine;
using NLog;

namespace WebLog2SQL
{
    public static class Program
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public static void Main(string[] arguments)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Logger.Fatal(e.ExceptionObject as Exception, "Unhandled Exception");
            Application.ThreadException += (s, e) => Logger.Error(e.Exception, "Unhandled Thread Exception");
            try
            {
                Logger.Warn(Environment.CommandLine);
                var agent = new Importer();
                Parser.Default.ParseArguments(arguments, agent.Options);
                agent.Import();
            }
            catch (Exception ex) when (!Break()) { Logger.Fatal(ex, "Fatal Error"); }
            finally { Logger.Warn("Done."); }
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
