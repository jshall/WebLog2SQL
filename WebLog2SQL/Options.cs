using CommandLine;
using CommandLine.Text;

namespace WebLog2SQL
{
    internal class Options
    {
        [Option("db", DefaultValue = @"Server=(localdb)\MSSQLLocalDB;Database=WebLog", HelpText = "ConnectionString for the SQL database.")]
        public string ConnectionString { get; set; }

        [Option('k', "keep", HelpText = "Do not delete any existing entries.")]
        public bool Keep { get; set; }

        [Option('b', "BufferSize", DefaultValue = 10000)]
        public int BufferSize { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = HelpText.AutoBuild(this);
            help.AddPreOptionsLine(@"Usage: WebLog2SQL [options]");
            return help;
        }
    }
}
