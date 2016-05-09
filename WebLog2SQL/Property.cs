using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebLog2SQL
{
    public class Property
    {
        [Key, MaxLength(20)]
        public byte[] Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public virtual ICollection<Event> Events { get; set; }
    }
}
