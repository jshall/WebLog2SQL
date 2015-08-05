using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using FileHook = System.Tuple<WebLog2SQL.Location, System.IO.FileInfo>;

namespace WebLog2SQL
{
    public class Location
    {
        [Key]
        public string Name { get; set; }
        [Required]
        public string Root { get; set; }
        public int DaysToKeep { get; set; }
        public string Exclude { get; set; }
        public string Filter { get; set; }

        public virtual ICollection<File> Files { get; set; }

        private string[] _list;
        private DateTimeOffset _cutoff;
        public IEnumerable<FileHook> Search()
        {
            var dir = new DirectoryInfo(Root);
            if (!dir.Exists) yield break;
            _cutoff = DateTime.Now.AddDays(0 - DaysToKeep);
            _list = (Exclude ?? "").Split(',').Select(x => x.Trim()).ToArray();
            foreach (var hook in Search(dir))
                yield return hook;
        }
        private IEnumerable<FileHook> Search(DirectoryInfo dir)
        {
            foreach (var hook in dir.GetDirectories()
                                    .Where(d => !_list.Contains(d.Name))
                                    .SelectMany(Search))
                yield return hook;
            foreach (var file in dir.GetFiles(Filter ?? "*.log", SearchOption.TopDirectoryOnly)
                                    .Where(file => file.LastWriteTime > _cutoff))
                yield return new FileHook(this, file);
        }
    }
}