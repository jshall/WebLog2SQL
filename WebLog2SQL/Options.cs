using CommandLine;
using CommandLine.Text;

namespace WebLog2SQL
{
    public class Options
    {
        [Option("db", HelpText = "ConnectionString for the SQL database.")]
        public string ConnectionString { get; set; } = @"Server=(localdb)\MSSQLLocalDB;Database=WebLog";

        [Option('k', "keep", HelpText = "Do not delete any existing entries.")]
        public bool Keep { get; set; }

        [Option('b', "BufferSize")]
        public int BufferSize { get; set; } = 10000;

        [HelpOption]
        public string GetUsage()
        {
            var help = HelpText.AutoBuild(this);
            help.AddPreOptionsLine(@"Usage: WebLog2SQL [options]");
            return help;
        }
    }
}
