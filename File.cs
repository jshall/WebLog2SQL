using System;
using System.ComponentModel.DataAnnotations;

namespace WebLog2SQL
{
    internal partial class File
    {
        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable UnusedAutoPropertyAccessor.Global
        public long Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset Updated { get; set; }
        public DateTimeOffset? Scanned { get; set; }
        public long BytesRead { get; set; }
        public string LastFields { get; set; }
        public int EventCount { get; set; }
        // ReSharper restore UnusedAutoPropertyAccessor.Global
        // ReSharper restore MemberCanBePrivate.Global
    }
}
