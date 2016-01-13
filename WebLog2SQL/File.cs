using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Migrations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace WebLog2SQL
{
    public partial class File : IDisposable
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
        // ReSharper restore UnusedAutoPropertyAccessor.Global
        // ReSharper restore MemberCanBePrivate.Global

        public string FullName => $@"{LocationName}\{Path}\{Name}";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly SemaphoreSlim Pool = new SemaphoreSlim(100, 100);
        private WebLogDB _ctx;

        public static File From(Location loc, FileInfo fileInfo)
        {
            var path = fileInfo.DirectoryName.Replace(loc.Root, "").TrimStart('\\');
            Pool.WaitAsync().Wait();
            var ctx = new WebLogDB();
            var file = ctx.Files.SingleOrDefault(f => f.LocationName == loc.Name && f.Path == path && f.Name == fileInfo.Name)
                       ?? new File { LocationName = loc.Name, Path = path, Name = fileInfo.Name, Created = fileInfo.CreationTime };
            file.Updated = fileInfo.LastWriteTime;
            ctx.Files.AddOrUpdate(file);
            ctx.SaveChanges();
            file._ctx = ctx;
            file._file = fileInfo;
            return file;
        }

        private FileInfo _file;

        public async Task ImportAsync()
        {
            var lineCount = 0;
            try
            {
                _file.Refresh();
                if (_file.Length <= 0L || BytesRead >= _file.Length) return;
                using (var stream = _file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var txt = new StreamReader(stream))
                {
                    var buffer =
                        LastFields == null
                            ? null
                            : new Events(this, "#Fields: " + LastFields);
                    stream.Position = BytesRead;
                    for (lineCount = EventCount + 1; !txt.EndOfStream; lineCount++)
                    {
                        var line = await txt.ReadLineAsync() ?? "";
                        BytesRead = stream.Position + (int)txt.GetField("charPos") - (int)txt.GetField("charLen");
                        if (line.StartsWith("#Fields: ") && buffer?.Header != line)
                            buffer = new Events(this, line);
                        else if (line[0] == '#') continue;
                        else
                            try { await buffer.AddAsync(line); }
                            catch (Exception ex) { Logger.Error(ex, "{0}:{1} Could not add to buffer: {2}", FullName, lineCount, line); }
                    }
                    if (buffer != null)
                        await buffer.FlushAsync();
                }
            }
            catch (Exception ex) { Logger.Error(ex, "{0}:{1} {2}", FullName, lineCount, ex.Message); }
            finally { Dispose(); }
        }

        public void Dispose()
        {
            _ctx.Dispose();
            Pool.Release();
        }
    }
}