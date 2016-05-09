using System;
using System.Collections.Generic;

namespace WebLog2SQL
{
    public class Event
    {
        public long Id { get; set; }
        public long FileId { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public virtual File File { get; set; }
        public virtual ICollection<Property> Properties { get; set; }
    }
}