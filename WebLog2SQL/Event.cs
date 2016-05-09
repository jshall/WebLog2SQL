using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            foreach (var prop in ParseFields(File.LastFields).Zip(ParseFields(line), (n, v) => new { Name = n, Value = v }))
                try
                {
                    if (prop.Name.Matches("date"))
                        Timestamp =
                            new DateTimeOffset(
                                DateTime.Parse(prop.Value) + Timestamp.ToOffset(File.Location.OffsetGMT).TimeOfDay,
                                File.Location.OffsetGMT);
                    else if (prop.Name.Matches("time"))
                        Timestamp =
                            new DateTimeOffset(
                                Timestamp.ToOffset(File.Location.OffsetGMT).Date + TimeSpan.Parse(prop.Value),
                                File.Location.OffsetGMT);
                    else
                    {
                        if (Properties == null) Properties = new List<Property>();
                        Properties.Add(await Property.Helper.CreateAsync(ctx, prop.Name, prop.Value));
                    }
                }
                catch (Exception ex) when (!Program.Break())
                {
                    throw new Exception("Failed to save property.", ex);
                }
        }

        internal static IEnumerable<string> ParseFields(string line)
        {
            var inQuotes = false;
            var buffer = new StringBuilder();
            for (var i = 0; i < line.Length; i++)
                if (line[i] == '"' && buffer.Length == 0)
                    inQuotes = true;
                else if (inQuotes && char.IsWhiteSpace(line, i))
                    buffer.Append(line[i]);
                else if (inQuotes && line[i] == '"' && i + 1 == line.Length)
                    inQuotes = false;
                else if (inQuotes && line[i] == '"' && char.IsWhiteSpace(line, i + 1))
                    inQuotes = false;
                else if (inQuotes && line[i] == '"' && line[i + 1] == '"')
                    buffer.Append(line[i++]);
                else if (inQuotes && line[i] == '"')
                    throw new ArgumentException("Invalid use of quotes.", line);
                else if (char.IsWhiteSpace(line, i))
                {
                    if (buffer.Length == 0) continue;
                    yield return buffer.ToString();
                    buffer.Clear();
                }
                else
                    buffer.Append(line[i]);
            if (buffer.Length > 0)
                yield return buffer.ToString();
        }
    }
}