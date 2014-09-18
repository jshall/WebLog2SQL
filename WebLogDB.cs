using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace WebLog2SQL
{
    internal partial class WebLogDB
    {
        public WebLogDB() : this(Program.Settings.ConnectionString) { }
    }

    internal partial class File
    {
        private static List<File> list;
        static File()
        {
            using (var db = new SqlConnection(Program.Settings.ConnectionString))
            {
                db.Open();
                _evtFlds = db
                    .GetSchema("Columns", new[] { null, null, "Event" })
                    .AsEnumerable()
                    .Select(c => c["COLUMN_NAME"] as string)
                    .ToArray();
                using (var ctx = new WebLogDB(db))
                    list = ctx.Files.ToList();
            }
        }

        public static File from(FileInfo info)
        {
            var file = list.FirstOrDefault(f => f.Name == info.FullName)
                     ?? new File { Name = info.FullName };
            using (var ctx = new WebLogDB())
            {
                if (file.Id == 0)
                    ctx.Files.InsertOnSubmit(file);
                else
                    ctx.Files.Attach(file);
                file.Updated = info.LastWriteTimeUtc;
                ctx.SubmitChanges();
            }
            file._file = info;
            return file;
        }

        private static string[] Values(string str)
        {
            return (str ?? "").Split(' ')
                              .Where(f => !string.IsNullOrWhiteSpace(f))
                              .Select(f => f == "-" ? null : f)
                              .ToArray();
        }

        protected FileInfo _file = null;
        protected DataTable _buffer = null;

        public void Import()
        {
            try
            {
                _file.Refresh();
                if (_file.Length <= 0L || BytesRead >= _file.Length) return;
                using (var txt = new StreamReader(_file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    txt.BaseStream.Position = BytesRead;
                    if (LastFields != null) Prepare();
                    for (var lineCount = EventCount + 1; !txt.EndOfStream; lineCount++)
                    {
                        var line = txt.ReadLine();
                        BytesRead = txt.BaseStream.Position + (int)txt.GetField("charPos") - (int)txt.GetField("charLen");
                        if (line.StartsWith("#Fields: ") && LastFields != line.Substring(9))
                        {
                            LastFields = line.Substring(9);
                            Prepare();
                        }
                        else if (line[0] != '#' && _buffer != null && Values(line).Length == _buffer.Columns.Count - 1)
                        {
                            _buffer.Rows.Add(Values(line));
                            if (_buffer != null && _buffer.Rows.Count == 10000)
                                Update();
                        }
                    }
                    Update();
                }
            }
            catch (Exception) { }
        }

        private static string[] _evtFlds = null;
        protected void Prepare()
        {
            Update();
            var fields = Values(LastFields);
            lock (_evtFlds)
            {
                var add = fields.Except(_evtFlds);
                if (add.Any())
                {
                    add = add.Select(f => string.Format("[{0}] varchar(max) NULL", f)).ToArray();
                    using (var ctx = new WebLogDB())
                        ctx.ExecuteCommand("ALTER TABLE [Event] ADD " + string.Join(", ", add));
                    _evtFlds = _evtFlds.Concat(add).ToArray();
                }
            }
            _buffer = new DataTable();
            foreach (var f in fields)
                _buffer.Columns.Add(f, typeof(string));
            _buffer.Columns.Add("FileID", typeof(long), Id.ToString());
        }

        protected void Update()
        {
            Scanned = DateTime.UtcNow;
            using (var ctx = new WebLogDB())
            {
                if (_buffer != null)
                    using (var bulkCopy = new SqlBulkCopy((SqlConnection)ctx.Connection))
                    {
                        bulkCopy.BulkCopyTimeout = 300;
                        bulkCopy.DestinationTableName = "Event";
                        foreach (DataColumn col in _buffer.Columns)
                            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                        if (ctx.Connection.State != ConnectionState.Open)
                            ctx.Connection.Open();
                        bulkCopy.WriteToServer(_buffer);
                        if (Program.Settings.Verbose)
                            Console.WriteLine("Imported {0,6:#,##0} events from {1}.", _buffer.Rows.Count, _file.FullName);
                        EventCount += _buffer.Rows.Count;
                        _buffer.Rows.Clear();
                    }
                ctx.ExecuteCommand(
                    "update [File] set LastFields={1}, BytesRead={2}, EventCount={3}, Scanned={4} where ID={0}",
                    _Id, _LastFields, _BytesRead, _EventCount, _Scanned);
            }
        }
    }
}
