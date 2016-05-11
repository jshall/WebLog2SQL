using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    }
}