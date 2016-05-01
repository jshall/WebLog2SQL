using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebLog2SQL
{
    public class Event
    {
        // ReSharper disable UnusedMember.Global
        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable UnusedAutoPropertyAccessor.Global
        public long Id { get; set; }
        public long FileId { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public File File { get; set; }

        public Event() { }
        // ReSharper restore UnusedAutoPropertyAccessor.Global
        // ReSharper restore MemberCanBePrivate.Global

        [NotMapped]
        public string date
        {
            get { return Timestamp.ToString("yyyy-MM-dd"); }
            set { Timestamp = DateTime.Parse(value) + Timestamp.TimeOfDay; }
        }
        [NotMapped]
        public string time
        {
            get { return Timestamp.ToString("yyyy-MM-dd"); }
            set { Timestamp = DateTime.Parse(value) + Timestamp.TimeOfDay; }
        }
        // ReSharper restore UnusedMember.Global

        public Event(File file, string line)
        {
            throw new NotImplementedException();
        }
    }
}