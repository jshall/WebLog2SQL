using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WebLog2SQL
{
    internal partial class WebLogDB
    {
        public WebLogDB() : this(Program.Settings.ConnectionString) { }

        private static JavaScriptSerializer json = new JavaScriptSerializer();
        internal void ImportUA(IEnumerable<string> UAStrings)
        {
            UAStrings = from UAS in UAStrings.Distinct()
                        join ua in UserAgents on UAS equals ua.agent_string into l
                        where !l.Any() && !string.IsNullOrWhiteSpace(UAS)
                        select UAS;
            foreach (var UAS in UAStrings)
            {
                var data = new System.Net.WebClient()
                              .DownloadString("http://www.useragentstring.com/?getJSON=all&uas=" + UAS)
                              .Replace("\"\"", "null")
                              .Replace("\"unknown\"", "null");
                var ua = json.Deserialize<UserAgent>(data);
                ua.agent_string = UAS;
                ua.agent_name = ua.agent_name ?? UAS.Replace('+', ' ');
                UserAgents.InsertOnSubmit(ua);
            }
            SubmitChanges();
        }
        internal void ImportGeo(IEnumerable<string> IPs)
        {
            IPs = from IP in IPs.Distinct()
                  join g in GeoDatas on IP equals g.ip into l
                  where !l.Any() && !string.IsNullOrWhiteSpace(IP)
                  select IP;
            foreach (var IP in IPs)
            {
                var data = new System.Net.WebClient()
                              .DownloadString("http://freegeoip.net/json/" + IP)
                              .Replace("\"\"", "null")
                              .Replace("\"unknown\"", "null");
                var geo = json.Deserialize<GeoData>(data);
                GeoDatas.InsertOnSubmit(geo);
            }
            SubmitChanges();
        }
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
            int lineCount = 0;
            try
            {
                _file.Refresh();
                if (_file.Length <= 0L || BytesRead >= _file.Length) return;
                using (var txt = new StreamReader(_file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    var IPs = new List<string>();
                    var UAs = new List<string>();
                    txt.BaseStream.Position = BytesRead;
                    if (LastFields != null) Prepare();
                    for (lineCount = EventCount + 1; !txt.EndOfStream; lineCount++)
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
                            if (_colIP >= 0) IPs.Add(Values(line)[_colIP]);
                            if (_colUA >= 0) UAs.Add(Values(line)[_colUA]);
                            if (_buffer != null && _buffer.Rows.Count == 10000)
                                Update();
                        }
                        else Debug.WriteLine("{0}:{1} Could not parse: {2}", _file.FullName, lineCount, line);
                    }
                    Update();
                    using (var ctx = new WebLogDB())
                        lock (_evtFlds)
                        {
                            ctx.ImportGeo(IPs);
                            ctx.ImportUA(UAs);
                        }
                }
            }
            catch (Exception ex) { Debug.WriteLine("{0}:{1} {2}", _file.FullName, lineCount, ex); }
        }

        private static string[] _evtFlds = null;
        private int _colIP = -1, _colUA = -1;
        protected void Prepare()
        {
            Update();
            var fields = Values(LastFields);
            _colIP = Array.IndexOf(fields, "c-ip");
            _colUA = Array.IndexOf(fields, "cs(User-Agent)");
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
                lock (_evtFlds)
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
