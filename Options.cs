using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace WebLog2SQL
{
    internal class Options
    {
        [Option("database", DefaultValue = @"Data Source=.\SQLEXPRESS;Initial Catalog=WebLog;Integrated Security=True")]
        public string ConnectionString { get; set; }

        private static DateTime _loaded = DateTime.Now;
        [Option('d', "days", DefaultValue = 60, HelpText = "Number of days to import.")]
        public int days { get; set; }
        public DateTime MaxAge { get { return _loaded.AddDays(-days); } }

        [Option('r', "remove", HelpText = "Remove files update prior to the import cut off.")]
        public bool Clean { get; set; }

        [OptionList('x', "exclude", HelpText = "Directories to exclude from import.")]
        public IList<string> Exclude { get; set; }

        [Option('f', "filter", DefaultValue = "*.log", HelpText = "The file filter applied to each directory.")]
        public string Filter { get; set; }

        [Option('v', "verbose")]
        public bool Verbose { get; set; }

        [ValueList(typeof(List<string>))]
        public IList<string> Include { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var help = HelpText.AutoBuild(this);
            help.AddPreOptionsLine(@"Usage: WebLog2SQL [options] \\server1\path\to\logs [\\server2\path\to\logs ...]");
            return help;
        }
    }
}
