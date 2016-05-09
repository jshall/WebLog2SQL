using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebLog2SQL
{
    public class Event
    {
        public long Id { get; set; }
        public long FileId { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public virtual File File { get; set; }
        public virtual ICollection<Property> Properties { get; set; }

        internal async Task FillAsync(WebLogDB ctx, string line)
        {
            var names = File.LastFields.Split(' ');
            var values = line.Split(' ');

            for (var i = 0; i < names.Length; i++)
                try
                {
                    if (names[i].Matches("date"))
                        Timestamp =
                            new DateTimeOffset(
                                DateTime.Parse(values[i]) + Timestamp.ToOffset(File.Location.OffsetGMT).TimeOfDay,
                                File.Location.OffsetGMT);
                    else if (names[i].Matches("time"))
                        Timestamp =
                            new DateTimeOffset(
                                Timestamp.ToOffset(File.Location.OffsetGMT).Date + TimeSpan.Parse(values[i]),
                                File.Location.OffsetGMT);
                    else
                    {
                        if (Properties == null) Properties = new List<Property>();
                        Properties.Add(await Property.Helper.CreateAsync(ctx, names[i], values[i]));
                    }
                }
                catch (Exception ex) when (!Program.Break())
                {
                    throw new Exception("Failed to save property.", ex);
                }
        }
    }
}