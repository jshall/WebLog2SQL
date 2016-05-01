using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Migrations;
using EntityFramework.BulkInsert.Extensions;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace WebLog2SQL
{
    public class File
    {
        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable UnusedAutoPropertyAccessor.Global
        public long Id { get; set; }

        [Required, StringLength(128), Index("IX_Files_FullName", 1, IsUnique = true), Index("IX_Files_Location")]
        public string LocationName { get; set; }

        [Required, StringLength(512), Index("IX_Files_FullName", 2, IsUnique = true), Index("IX_Files_Path")]
        public string Path { get; set; }

        [Required, StringLength(128), Index("IX_Files_FullName", 3, IsUnique = true), Index("IX_Files_Name")]
        public string Name { get; set; }

        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Updated { get; set; }
        public DateTimeOffset? Scanned { get; set; }
        public long BytesRead { get; set; }
        public string LastFields { get; set; }
        public int EventCount { get; set; }

        public Location Location { get; set; }
        public virtual ICollection<Event> Events { get; set; }
        // ReSharper restore UnusedAutoPropertyAccessor.Global
        // ReSharper restore MemberCanBePrivate.Global

        public string FullName => $@"{LocationName}\{Path}\{Name}";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly SemaphoreSlim Pool = new SemaphoreSlim(100, 100);
        public static async Task ImportAsync(Location loc, FileInfo fileInfo)
        {
            File file = null;
            var lineCount = 0;
            var path = fileInfo.DirectoryName?.Replace(loc.Root ?? "", "").TrimStart('\\');
            Pool.WaitAsync().Wait();
            using (var ctx = new WebLogDB())
                try
                {
                    file = ctx.Files.SingleOrDefault(f =>
                            f.LocationName == loc.Name &&
                            f.Path == path &&
                            f.Name == fileInfo.Name)
                        ?? new File
                        {
                            LocationName = loc.Name,
                            Path = path,
                            Name = fileInfo.Name,
                            Created = fileInfo.CreationTime
                        };
                    file.Updated = fileInfo.LastWriteTime;
                    ctx.Files.AddOrUpdate(file);
                    ctx.SaveChanges();
                    fileInfo.Refresh();
                    if (fileInfo.Length <= 0L || file.BytesRead >= fileInfo.Length) return;
                    using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var txt = new StreamReader(stream))
                    using (var buffer = new Buffer(ctx, file))
                    {
                        stream.Position = file.BytesRead;
                        for (lineCount = file.EventCount + 1; !txt.EndOfStream; lineCount++)
                        {
                            var line = await txt.ReadLineAsync() ?? "";
                            file.BytesRead = stream.Position + (int)txt.GetField("charPos") - (int)txt.GetField("charLen");
                            if (line.StartsWith("#Fields: "))
                                file.LastFields = line.Substring(9);
                            else if (line[0] == '#') continue;
                            else
                                try { buffer.Add(new Event(file, line)); }
                                catch (Exception ex)
                                {
                                    Logger.Error(ex, "{0}:{1} Could not add to buffer: {2}", file.FullName, lineCount, line);
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (file == null)
                        Logger.Error(ex);
                    else
                        Logger.Error(ex, "{0}:{1} {2}", file.FullName, lineCount, ex.Message);
                }
                finally
                {
                    Pool.Release();
                }
        }

        internal class Buffer : Collection<Event>, IDisposable
        {
            private readonly File _file;
            private readonly WebLogDB _ctx;
            public Buffer(WebLogDB ctx, File file)
            {
                _ctx = ctx;
                _file = file;
            }

            public static int MaxRowsToBuffer { get; set; }
            public new void Add(Event item)
            {
                if (item.File != null && item.File != _file)
                    throw new ArgumentException("Event belongs to another File", nameof(item));
                _file.Events.Add(item);
                base.Add(item);
                if (Count >= MaxRowsToBuffer)
                    Flush();
            }

            public void Flush()
            {
                using (var trans = _ctx.Database.BeginTransaction())
                {
                    _ctx.BulkInsert(this, trans.UnderlyingTransaction);
                    _ctx.SaveChanges();
                    trans.Commit();
                    Logger.Info("Imported {0,6:#,##0} events from {1}.", Count, _file.FullName);
                }
                Clear();
            }

            public void Dispose()
            {
                Flush();
            }
        }
    }
}