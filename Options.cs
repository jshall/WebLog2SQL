using CommandLine;
using CommandLine.Text;

namespace WebLog2SQL
{
    internal class Options
    {
        [Option("db", DefaultValue = @"Server=(localdb)\v11.0;Database=WebLog", HelpText = "ConnectionString for the SQL database.")]
        public string ConnectionString { get; set; }

        [Option('k', "keep", HelpText = "Keep files updated outside the 'DaysToKeep'.")]
        public bool Keep { get; set; }

        [Option('v', "verbose")]
        public bool Verbose { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = HelpText.AutoBuild(this);
            help.AddPreOptionsLine(@"Usage: WebLog2SQL [options]");
            return help;
        }
    }
}
