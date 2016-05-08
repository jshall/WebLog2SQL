using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebLog2SQL
{
    public class Property
    {
        [Key, MaxLength(20)]
        public byte[] Id { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public virtual ICollection<EventProperty> EventProperties { get; set; }

        private static readonly WebLogDB Context = new WebLogDB();
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
        private static readonly Encoding Enc = new UnicodeEncoding();
        internal static async Task<Property> CreateAsync(string name, string value)
        {
            byte[] id;
            using (var sha = new SHA1CryptoServiceProvider())
            {
                var bytes = Enc.GetBytes(name).ToList();
                bytes.Add(0);
                bytes.AddRange(Enc.GetBytes(value));
                id = sha.ComputeHash(bytes.ToArray());
            }
            await Lock.WaitAsync();
            var prop = await Context.Properties.FirstOrDefaultAsync(p => p.Id == id);
            if (prop == null)
                Context.Properties.Add(prop = new Property { Id = id, Name = name, Value = value });
            await Context.SaveChangesAsync();
            Lock.Release();
            return prop;
        }
    }
}
