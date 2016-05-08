using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebLog2SQL
{
    public class EventProperty
    {
        [Key, Column(Order = 1)]
        public long EventId { get; set; }
        [Key, Column(Order = 0)]
        public byte[] PropertyId { get; set; }

        public virtual Event Event { get; set; }
    }
}