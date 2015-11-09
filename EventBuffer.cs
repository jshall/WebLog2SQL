using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace WebLog2SQL
{
    partial class File
    {
        internal class Events : IDisposable
        {
            private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
            private static readonly List<string> DatabaseFields;

            static Events()
            {
                using (var ctx = new WebLogDB())
                {
                    ctx.Database.Connection.Open();
                    Logger.Warn("Preparing Events Table");
                    ctx.Database.ExecuteSqlCommand(
                        "IF OBJECT_ID('dbo.Events') IS NULL " +
                            "BEGIN " +
                                "CREATE TABLE [dbo].[Events] (" +
                                    "[EventId] BIGINT IDENTITY," +
                                    "[FileId] BIGINT NOT NULL," +
                                    "[datetime] AS dateadd(day,datediff(day,'19000101',[date]),CONVERT([datetimeoffset](7),[time],(0)))," +
                                    "[date] DATE NOT NULL," +
                                    "[time] TIME(7) NOT NULL," +
                                    "[cs-bytes] INT NULL," +
                                    "[s-port] INT NULL," +
                                    "[sc-status] INT NULL," +
                                    "[sc-substatus] INT NULL," +
                                    "[sc-win32-status] BIGINT NULL," +
                                    "[sc-bytes] INT NULL," +
                                    "[time-taken] INT NULL " +
                                    "CONSTRAINT [PK_Events] PRIMARY KEY NONCLUSTERED ([EventId])" +
                                    "CONSTRAINT [FK_Events_Files_FileId] FOREIGN KEY ([FileId])" +
                                        "REFERENCES [dbo].[Files] ([Id]) ON DELETE CASCADE);" +
                                "CREATE CLUSTERED INDEX [IX_Events_Order] " +
                                    "ON [dbo].[Events]([FileID] ASC, [date] ASC, [time] ASC);" +
                                "CREATE NONCLUSTERED INDEX [IX_Events_FileId] " +
                                    "ON [dbo].[Events]([FileId]);" +
                            "END");
                    DatabaseFields = ctx.Database.Connection
                        .GetSchema("Columns", new[] { null, null, "Events" })
                        .AsEnumerable()
                        .Select(c => c["COLUMN_NAME"] as string)
                        .ToList();
                }
            }

            private readonly File _file;
            private readonly DataTable _data = new DataTable();
            public readonly string Header;
            public static int MaxRowsToBuffer { get; set; } = 10000;

            public Events(File file, string header)
            {
                _file = file;
                Header = header;
                _file.LastFields = header.Substring(9);
                var fields = ParseLine(header.Substring(9)).ToArray();
                var add = fields.Except(DatabaseFields, StringComparer.CurrentCultureIgnoreCase).ToArray();
                if (add.Any())
                {
                    _file._ctx.Database.ExecuteSqlCommand(
                        "ALTER TABLE Events ADD " +
                        string.Join(", ", from f in add select $"[{f}] nvarchar(4000) NULL")
                        );
                    DatabaseFields.AddRange(add);
                }
                foreach (var f in fields)
                    _data.Columns.Add(f, typeof(string));
                _data.Columns.Add("FileId", typeof(long), _file.Id.ToString());
            }

            public async Task AddAsync(string line)
            {
                var values = ParseLine(line).ToArray();
                if (values.Length + 1 != _data.Columns.Count)
                    throw new ArgumentException("There are an unexpected number of fields.", nameof(line));
                _data.Rows.Add(values);
                if (_data.Rows.Count >= MaxRowsToBuffer)
                    await FlushAsync();
            }

            private static string Filter(StringBuilder s) => s.Length == 1 && s[0] == '-' ? null : s.ToString().Replace("+", " ");
            private static IEnumerable<string> ParseLine(string line)
            {
                line = line.Trim();
                var tmp = new StringBuilder(100, 4000);
                restart:
                var ignore = false;
                for (var i = 0; i < line.Length; i++)
                    if (line[i] != '"' && (ignore || line[i] != ' '))
                        tmp.Append(line[i]);
                    else if (line[i] == ' ')
                    {
                        yield return Filter(tmp);
                        tmp.Clear();
                    }
                    else if (tmp.Length == 0)
                        ignore = true;
                    else if (i + 1 == line.Length || line[i + 1] == ' ')
                        ignore = false;
                    else if (line[i + 1] == '"')
                        tmp.Append(line[i++]);
                    else
                        tmp.Append(line[i]);
                if (ignore)
                {
                    line = tmp.ToString();
                    tmp.Clear().Append('"');
                    goto restart;
                }
                yield return Filter(tmp);
            }

            public async Task FlushAsync()
            {
                _file.Scanned = DateTime.Now;
                var db = (SqlConnection)_file._ctx.Database.Connection;
                using (var bulkCopy = new SqlBulkCopy(db))
                {
                    bulkCopy.BulkCopyTimeout = 300;
                    bulkCopy.DestinationTableName = "Events";
                    foreach (DataColumn col in _data.Columns)
                        bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    if (db.State != ConnectionState.Open)
                        db.Open();
                    await bulkCopy.WriteToServerAsync(_data);
                }
                _file.EventCount += _data.Rows.Count;
                var save = _file._ctx.SaveChangesAsync();
                Logger.Info("Imported {0,6:#,##0} events from {1}.", _data.Rows.Count, _file.FullName);
                _data.Clear();
                await save;
            }

            public void Dispose()
            {
                FlushAsync().Wait();
            }
        }
    }
}
