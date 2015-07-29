using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebLog2SQL
{
    internal partial class WebLogDB { public WebLogDB() : this(Program.Settings.ConnectionString) { } }
    internal partial class File
    {
        private static readonly List<File> List;
        static File()
        {
            using (var db = new SqlConnection(Program.Settings.ConnectionString))
            {
                db.Open();
                EventFieldList = db
                    .GetSchema("Columns", new[] { null, null, "Event" })
                    .AsEnumerable()
                    .Select(c => c["COLUMN_NAME"] as string)
                    .ToList();
                using (var ctx = new WebLogDB(db))
                    List = ctx.Files.ToList();
            }
        }

        public static File From(FileInfo info)
        {
            var file = List.FirstOrDefault(f => f.Name == info.FullName)
                     ?? new File { Name = info.FullName, Created = info.CreationTime };
            using (var ctx = new WebLogDB())
            {
                if (file.Id == 0)
                    ctx.Files.InsertOnSubmit(file);
                else
                    ctx.Files.Attach(file);
                file.Updated = info.LastWriteTime;
                ctx.SubmitChanges();
            }
            file._file = info;
            return file;
        }

        private static readonly Regex ValueRegex = new Regex(@"[\S-[""]]+|""((?:[^""]|(?<=\\)"")*)""", RegexOptions.Compiled);
        private static IEnumerable<string> Values(string str)
        {
            var match = ValueRegex.Match(str ?? "");
            while (match.Success)
            {
                var val = match.Value.Trim('"');
                yield return val == "-" ? null : val.Length > 4000 ? val.Substring(0, 3997) + "..." : val;
                match = match.NextMatch();
            }
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
                        var values = Values(line).ToArray();
                        if (values[0] == "#Fields:" && LastFields != line.Substring(9))
                        {
                            LastFields = line.Substring(9);
                            Prepare();
                        }
                        else if (line[0] == '#') continue;
                        else if (_buffer != null && values.Length + 1 == _buffer.Columns.Count)
                        {
                            _buffer.Rows.Add(values);
                            if (_buffer != null && _buffer.Rows.Count == 10000)
                                Prepare();
                        }
                        else Debug.WriteLine("{0}:{1} Could not parse: {2}", _file.FullName, lineCount, line);
                    }
                    Update(_buffer);
                }
            }
            catch (Exception ex) { Debug.WriteLine("{0}:{1} {2}", _file.FullName, lineCount, ex); }
        }

        private static readonly List<string> EventFieldList;
        private void Prepare()
        {
            new System.Threading.Thread(Update).Start(_buffer);
            var fields = Values(LastFields).ToArray();
            lock (EventFieldList)
            {
                var add = fields.Except(EventFieldList).ToArray();
                if (add.Any())
                {
                    using (var ctx = new WebLogDB())
                        ctx.ExecuteCommand("ALTER TABLE [Event] ADD " +
                            string.Join(", ", add.Select(f => string.Format("[{0}] nvarchar(4000) NULL", f))));
                    EventFieldList.AddRange(add);
                }
            }
            _buffer = new DataTable();
            foreach (var f in fields)
                _buffer.Columns.Add(f, typeof(string));
            _buffer.Columns.Add("FileID", typeof(long), Id.ToString());
        }

        private void Update(object state)
        {
            var data = (DataTable)state;
            Scanned = DateTime.Now;
            using (var ctx = new WebLogDB())
                lock (EventFieldList)
                {
                    if (data != null)
                        using (var bulkCopy = new SqlBulkCopy((SqlConnection)ctx.Connection))
                        {
                            bulkCopy.BulkCopyTimeout = 300;
                            bulkCopy.DestinationTableName = "Event";
                            foreach (DataColumn col in data.Columns)
                                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            if (ctx.Connection.State != ConnectionState.Open)
                                ctx.Connection.Open();
                            bulkCopy.WriteToServer(data);
                            if (Program.Settings.Verbose)
                                Console.WriteLine("Imported {0,6:#,##0} events from {1}.", data.Rows.Count, _file.FullName);
                            EventCount += data.Rows.Count;
                            data.Dispose();
                        }
                    ctx.ExecuteCommand(
                        "update [File] set LastFields={1}, BytesRead={2}, EventCount={3}, Scanned={4} where ID={0}",
                        _Id, _LastFields, _BytesRead, _EventCount, _Scanned);
                }
        }
    }
}
