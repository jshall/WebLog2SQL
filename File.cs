using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FileHook = System.Tuple<WebLog2SQL.Location, System.IO.FileInfo>;

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

        public virtual Location Location { get; set; }
        // ReSharper restore UnusedAutoPropertyAccessor.Global
        // ReSharper restore MemberCanBePrivate.Global

        public string FullName { get { return LocationName + '\\' + Path + '\\' + Name; } }

        static File()
        {
            using (var ctx = new WebLogDB())
            {
                var db = ctx.Database.Connection;
                db.Open();
                EventFieldList = db
                    .GetSchema("Columns", new[] { null, null, "Events" })
                    .AsEnumerable()
                    .Select(c => c["COLUMN_NAME"] as string)
                    .ToList();
            }
        }

        private WebLogDB _ctx;

        public static File From(FileHook hook)
        {
            lock (EventFieldList)
            {
                var ctx = new WebLogDB();
                var path = hook.Item2.DirectoryName.Replace(hook.Item1.Root, "").TrimStart('\\');
                var file = ctx.Files.SingleOrDefault(f => f.LocationName == hook.Item1.Name && f.Path == path && f.Name == hook.Item2.Name)
                           ?? new File { LocationName = hook.Item1.Name, Path = path, Name = hook.Item2.Name, Created = hook.Item2.CreationTime };
                file.Updated = hook.Item2.LastWriteTime;
                ctx.Files.AddOrUpdate(file);
                ctx.SaveChanges();
                file._ctx = ctx;
                file._file = hook.Item2;
                return file;
            }
        }

        private static string Filter(StringBuilder s) { return s.Length == 1 && s[0] == '-' ? null : s.ToString(); }
        private static IEnumerable<string> Values(string str)
        {
            var tmp = new StringBuilder(100, 4000);
        restart:
            var ignore = false;
            for (var i = 0; i < str.Length; i++)
                if (str[i] != '"' && (ignore || str[i] != ' '))
                    tmp.Append(str[i]);
                else if (str[i] == ' ')
                {
                    yield return Filter(tmp);
                    tmp.Clear();
                }
                else if (tmp.Length == 0)
                    ignore = true;
                else if (i + 1 == str.Length || str[i + 1] == ' ')
                    ignore = false;
                else if (str[i + 1] == '"')
                    tmp.Append(str[i++]);
                else
                    tmp.Append(str[i]);
            if (ignore)
            {
                str = tmp.ToString();
                tmp.Clear().Append('"');
                goto restart;
            }
            yield return Filter(tmp);
        }

        private FileInfo _file;
        private DataTable _buffer;

        public void Import()
        {
            var lineCount = 0;
            try
            {
                _file.Refresh();
                if (_file.Length <= 0L || BytesRead >= _file.Length) return;
                using (var stream = _file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var txt = new StreamReader(stream))
                {
                    stream.Position = BytesRead;
                    if (LastFields != null) Prepare();
                    for (lineCount = EventCount + 1; !txt.EndOfStream; lineCount++)
                    {
                        var line = txt.ReadLine() ?? "";
                        BytesRead = stream.Position + (int)txt.GetField("charPos") - (int)txt.GetField("charLen");
                        var values = Values(line.Trim(' ')).ToArray();
                        if (values[0] == "#Fields:" && LastFields != line.Substring(9))
                        {
                            LastFields = line.Substring(8).Trim(' ');
                            Prepare();
                        }
                        // ReSharper disable once RedundantJumpStatement
                        else if (line[0] == '#') continue;
                        else if (_buffer != null && values.Length + 1 == _buffer.Columns.Count)
                        {
                            // ReSharper disable once CoVariantArrayConversion
                            _buffer.Rows.Add(values);
                            if (_buffer != null && _buffer.Rows.Count == 10000)
                                Prepare();
                        }
                        else Debug.WriteLine("{0}:{1} Could not parse: {2}", FullName, lineCount, line);
                    }
                    Update();
                }
            }
            catch (Exception ex) { Debug.WriteLine("{0}:{1} {2}", FullName, lineCount, ex); }
        }

        private static readonly List<string> EventFieldList;
        private void Prepare()
        {
            Update();
            var fields = Values(LastFields).ToArray();
            lock (EventFieldList)
            {
                var add = fields.Except(EventFieldList).ToArray();
                if (add.Any())
                {
                    _ctx.Database.ExecuteSqlCommand("ALTER TABLE Events ADD " +
                        string.Join(", ", add.Select(f => string.Format("[{0}] nvarchar(4000) NULL", f))));
                    EventFieldList.AddRange(add);
                }
            }
            _buffer = new DataTable();
            foreach (var f in fields)
                _buffer.Columns.Add(f, typeof(string));
            _buffer.Columns.Add("FileId", typeof(long), Id.ToString());
        }

        private void Update()
        {
            Scanned = DateTime.Now;
            lock (EventFieldList)
            {
                if (_buffer != null)
                    using (var bulkCopy = new SqlBulkCopy((SqlConnection)_ctx.Database.Connection))
                    {
                        bulkCopy.BulkCopyTimeout = 300;
                        bulkCopy.DestinationTableName = "Events";
                        foreach (DataColumn col in _buffer.Columns)
                            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                        if (_ctx.Database.Connection.State != ConnectionState.Open)
                            _ctx.Database.Connection.Open();
                        bulkCopy.WriteToServer(_buffer);
                        if (Program.Settings.Verbose)
                            Console.WriteLine("Imported {0,6:#,##0} events from {1}.", _buffer.Rows.Count, FullName);
                        EventCount += _buffer.Rows.Count;
                        _buffer.Dispose();
                    }
                _ctx.SaveChanges();
            }
        }
    }
}
