using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebLog2SQL
{
    public class File
    {
        public long Id { get; set; }

        [MaxLength(128), Index("IX_FullName", 1, IsUnique = true), Index]
        public string LocationName { get; set; }

        [MaxLength(512), Index("IX_FullName", 2, IsUnique = true), Index]
        public string Path { get; set; }

        [MaxLength(128), Index("IX_FullName", 3, IsUnique = true), Index]
        public string Name { get; set; }

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Updated { get; set; }
        public DateTimeOffset? Scanned { get; set; }
        public long BytesRead { get; set; }
        public string LastFields { get; set; }

        public virtual Location Location { get; set; }
        public virtual ICollection<Event> Events { get; set; }

        public string FullName => $@"{LocationName}\{Path}\{Name}";
    }
}