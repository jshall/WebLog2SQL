using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;

namespace WebLog2SQL
{
    public class Location
    {
        [Key]
        public string Name { get; set; }
        [Required]
        public string Root { get; set; }
        public TimeSpan OffsetGMT { get; set; } = TimeSpan.Zero;
        public int DaysToKeep { get; set; }
        public string Exclude { get; set; }
        public string Filter { get; set; }

        public virtual ICollection<File> Files { get; set; }

        private DateTimeOffset _cutoff;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public IEnumerable<FileInfo> GetFiles()
        {
            var dir = new DirectoryInfo(Root);
            if (!dir.Exists)
            {
                Logger.Warn("Directory not found: {0}", dir.FullName);
                return new FileInfo[] { };
            }
            _cutoff = DateTime.Now.AddDays(-DaysToKeep);
            var ex = string.IsNullOrWhiteSpace(Exclude) ? null
                : new Regex(Exclude, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return dir.EnumerateFiles(Filter ?? "*.log", SearchOption.AllDirectories)
                      .Where(file => file.LastWriteTime > _cutoff)
                      .Where(file => !(ex?.Match(file.FullName).Success ?? false));
        }
    }
}