using CommandLine;
using CommandLine.Text;

namespace WebLog2SQL
{
    public class Options
    {
        internal const string DefaultConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=WebLog";
        [Option("db", HelpText = "ConnectionString for the SQL database.")]
        public string ConnectionString { get; set; } = DefaultConnectionString;

        [Option('k', "keep", HelpText = "Do not delete any existing entries.")]
        public bool Keep { get; set; }

        [Option('b', "BufferSize")]
        public int BufferSize { get; set; } = 100;

        [HelpOption]
        public string GetUsage()
        {
            var help = HelpText.AutoBuild(this);
            help.AddPreOptionsLine(@"Usage: WebLog2SQL [options]");
            return help;
        }
    }
}
