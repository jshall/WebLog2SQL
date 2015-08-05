using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WebLog2SQL.Migrations;

namespace WebLog2SQL
{
    internal class WebLogDB : DbContext
    {
        public WebLogDB()
            : base(Program.Settings.ConnectionString)
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<WebLogDB, Configuration>());
        }
        public DbSet<File> Files { get; set; }
    }

    internal partial class File
    {
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
        public static File From(FileInfo info)
        {
            lock (EventFieldList)
            {
                var ctx = new WebLogDB();
                var file = ctx.Files.SingleOrDefault(f => f.Name == info.FullName)
                           ?? new File { Name = info.FullName, Created = info.CreationTime };
                file.Updated = info.LastWriteTime;
                ctx.Files.Add(file);
                ctx.SaveChanges();
                file._ctx = ctx;
                file._file = info;
                return file;
            }
        }

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
                    yield return tmp.ToString();
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
            yield return tmp.ToString();
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
                        else Debug.WriteLine("{0}:{1} Could not parse: {2}", _file.FullName, lineCount, line);
                    }
                    Update();
                }
                _ctx.Dispose();
            }
            catch (Exception ex) { Debug.WriteLine("{0}:{1} {2}", _file.FullName, lineCount, ex); }
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
                            Console.WriteLine("Imported {0,6:#,##0} events from {1}.", _buffer.Rows.Count, _file.FullName);
                        EventCount += _buffer.Rows.Count;
                        _buffer.Dispose();
                    }
                _ctx.SaveChanges();
            }
        }
    }
}