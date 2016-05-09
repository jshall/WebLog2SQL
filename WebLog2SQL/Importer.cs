using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace WebLog2SQL
{
    public class Importer
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public readonly Options Options = new Options();

        public void Import()
        {
            IEnumerable<Location> locations;
            using (var ctx = new WebLogDB())
            {
                locations = ctx.Locations.AsNoTracking().ToArray();
                if (!Options.Keep)
                {
                    var list = from f in ctx.Files.ToArray()
                               where (f.Updated - DateTimeOffset.Now).TotalDays > f.Location.DaysToKeep
                               select f;
                    var originalTimeout = ctx.Database.CommandTimeout;
                    ctx.Database.CommandTimeout = 300;
                    foreach (var file in list)
                    {
                        Logger.Info($"Removing {file.FullName} (includes {file.Events?.Count ?? 0} events)");
                        ctx.Files.Remove(file);
                        ctx.SaveChanges();
                    }
                    ctx.Database.CommandTimeout = originalTimeout;
                }
            }

            Buffer.MaxRowsToBuffer = Options.BufferSize;
            Logger.Warn("Processing files");
            Task.WaitAll((
                from loc in locations.AsEnumerable()
                from file in GetFiles(loc)
                orderby file.CreationTime
                select ImportFileAsync(loc, file)
                ).ToArray());
        }

        private static IEnumerable<FileInfo> GetFiles(Location loc)
        {
            var dir = new DirectoryInfo(loc.Root);
            if (!dir.Exists)
            {
                Logger.Warn("Directory not found: {0}", dir.FullName);
                return new FileInfo[] { };
            }
            var cutoff = DateTime.Now.AddDays(-loc.DaysToKeep);
            var ex = String.IsNullOrWhiteSpace(loc.Exclude)
                ? null
                : new Regex(loc.Exclude, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return dir.EnumerateFiles(loc.Filter ?? "*.log", SearchOption.AllDirectories)
                .Where(file => file.LastWriteTime > cutoff)
                .Where(file => !(ex?.Match(file.FullName).Success ?? false));
        }

        #region File Helpers

        private static readonly SemaphoreSlim FilePool = new SemaphoreSlim(100, 100);

        private static async Task ImportFileAsync(Location loc, FileInfo fileInfo)
        {
            File file = null;
            var path = fileInfo.DirectoryName?.Replace(loc.Root ?? "", "").TrimStart('\\');
            await FilePool.WaitAsync();
            using (var ctx = new WebLogDB())
                try
                {
                    file = await ctx.Files.SingleOrDefaultAsync(f =>
                        f.LocationName == loc.Name &&
                        f.Path == path &&
                        f.Name == fileInfo.Name);
                    loc = await ctx.Locations.FindAsync(loc.Name);
                    if (file == null)
                        ctx.Files.Add(file = new File
                        {
                            Location = loc,
                            Path = path,
                            Name = fileInfo.Name,
                            Created = fileInfo.CreationTime
                        });
                    file.Updated = fileInfo.LastWriteTime;
                    await ctx.SaveChangesAsync();
                    fileInfo.Refresh();
                    if (fileInfo.Length <= 0L || file.BytesRead >= fileInfo.Length) return;
                    using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var txt = new StreamReader(stream))
                    using (var buffer = new Buffer(ctx, file))
                    {
                        stream.Position = file.BytesRead;
                        while (!txt.EndOfStream)
                        {
                            var line = await txt.ReadLineAsync() ?? "";
                            file.BytesRead = stream.Position + (int)txt.GetField("charPos") -
                                             (int)txt.GetField("charLen");
                            if (line.StartsWith("#Fields:"))
                                file.LastFields = line.Substring(8);
                            else if (line[0] == '#') continue;
                            else
                                try
                                {
                                    await buffer.Add(line);
                                }
                                catch (Exception ex) when (!Program.Break())
                                {
                                    Logger.Error(ex, "{0} Could not add to buffer: {1}", file.FullName, line);
                                }
                        }
                        await buffer.Flush();
                    }
                }
                catch (Exception ex) when (!Program.Break())
                {
                    if (file == null)
                        Logger.Error(ex);
                    else
                        Logger.Error(ex, "{0} {1}", file.FullName, ex.Message);
                }
                finally
                {
                    FilePool.Release();
                }
        }

        private class Buffer : IDisposable
        {
            private readonly WebLogDB _ctx;
            private readonly ICollection<Event> _events = new List<Event>();
            private readonly File _file;

            public Buffer(WebLogDB ctx, File file)
            {
                _ctx = ctx;
                _file = file;
            }

            public static int MaxRowsToBuffer { get; set; }

            public void Dispose()
            {
                if (_events.Count <= 0) return;
                throw new InvalidOperationException("Please flush the Buffer before disposing.");
            }

            internal async Task Add(string line)
            {
                var evt = new Event { File = _file };
                await FillEventAsync(_ctx, evt, line);

                _events.Add(evt);
                if (_events.Count >= MaxRowsToBuffer)
                    await Flush();
            }

            public async Task Flush()
            {
                _file.Scanned = DateTimeOffset.Now;
                _ctx.Events.AddRange(_events);
                await _ctx.SaveChangesAsync();
                Logger.Info("Imported {0,6:#,##0} events from {1}.", _events.Count, _file.FullName);
                _events.Clear();
            }
        }

        #endregion

        #region Event Helpers

        private static async Task FillEventAsync(WebLogDB ctx, Event evt, string line)
        {
            foreach (
                var prop in ParseEventFields(evt.File.LastFields).Zip(ParseEventFields(line), (n, v) => new { Name = n, Value = v })
                )
                try
                {
                    if (prop.Name.Matches("date"))
                        evt.Timestamp =
                            new DateTimeOffset(
                                DateTime.Parse(prop.Value) +
                                evt.Timestamp.ToOffset(evt.File.Location.OffsetGMT).TimeOfDay,
                                evt.File.Location.OffsetGMT);
                    else if (prop.Name.Matches("time"))
                        evt.Timestamp =
                            new DateTimeOffset(
                                evt.Timestamp.ToOffset(evt.File.Location.OffsetGMT).Date + TimeSpan.Parse(prop.Value),
                                evt.File.Location.OffsetGMT);
                    else
                    {
                        if (evt.Properties == null) evt.Properties = new List<Property>();
                        evt.Properties.Add(await GetPropertyAsync(ctx, prop.Name, prop.Value));
                    }
                }
                catch (Exception ex) when (!Program.Break())
                {
                    throw new Exception("Failed to save property.", ex);
                }
        }

        private static IEnumerable<string> ParseEventFields(string line)
        {
            var inQuotes = false;
            var buffer = new StringBuilder();
            for (var i = 0; i < line.Length; i++)
                if (line[i] == '"' && buffer.Length == 0)
                    inQuotes = true;
                else if (inQuotes && Char.IsWhiteSpace(line, i))
                    buffer.Append(line[i]);
                else if (inQuotes && line[i] == '"' && i + 1 == line.Length)
                    inQuotes = false;
                else if (inQuotes && line[i] == '"' && Char.IsWhiteSpace(line, i + 1))
                    inQuotes = false;
                else if (inQuotes && line[i] == '"' && line[i + 1] == '"')
                    buffer.Append(line[i++]);
                else if (inQuotes && line[i] == '"')
                    throw new ArgumentException("Invalid use of quotes.", line);
                else if (Char.IsWhiteSpace(line, i))
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

        #endregion

        #region Property Helpers

        private static readonly WebLogDB PropertyContext = new WebLogDB();
        private static readonly SemaphoreSlim PropertyLock = new SemaphoreSlim(1);
        private static readonly Encoding PropertyEncoding = new UnicodeEncoding();

        private static async Task<Property> GetPropertyAsync(WebLogDB ctx, string name, string value)
        {
            byte[] id;
            using (var sha = new SHA1CryptoServiceProvider())
            {
                var bytes = PropertyEncoding.GetBytes(name).ToList();
                bytes.Add(0);
                bytes.AddRange(PropertyEncoding.GetBytes(value));
                id = sha.ComputeHash(bytes.ToArray());
            }
            await PropertyLock.WaitAsync();
            var prop = await PropertyContext.Properties.FindAsync(id);
            var isNew = prop == null;
            if (isNew)
            {
                PropertyContext.Properties.Add(new Property { Id = id, Name = name, Value = value });
                await PropertyContext.SaveChangesAsync();
            }
            PropertyLock.Release();
            return await ctx.Properties.FindAsync(id);
        }

        #endregion
    }
}