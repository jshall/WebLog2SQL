using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

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

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly SemaphoreSlim Pool = new SemaphoreSlim(100, 100);
        public static async Task ImportAsync(Location loc, FileInfo fileInfo)
        {
            File file = null;
            var path = fileInfo.DirectoryName?.Replace(loc.Root ?? "", "").TrimStart('\\');
            await Pool.WaitAsync();
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
                            file.BytesRead = stream.Position + (int)txt.GetField("charPos") - (int)txt.GetField("charLen");
                            if (line.StartsWith("#Fields: "))
                                file.LastFields = line.Substring(9);
                            else if (line[0] == '#') continue;
                            else
                                try { await buffer.Add(line); }
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
                    Pool.Release();
                }
        }

        internal class Buffer : IDisposable
        {
            private readonly File _file;
            private readonly WebLogDB _ctx;
            private readonly ICollection<Event> _events = new List<Event>();
            public Buffer(WebLogDB ctx, File file)
            {
                _ctx = ctx;
                _file = file;
            }

            public static int MaxRowsToBuffer { get; set; }
            internal async Task Add(string line)
            {
                var evt = new Event { File = _file };
                await evt.FillAsync(_ctx, line);

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

            public void Dispose()
            {
                if (_events.Count <= 0) return;
                throw new InvalidOperationException("Please flush the Buffer before disposing.");
            }
        }
    }
}