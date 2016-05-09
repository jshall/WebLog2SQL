using System.Data.Entity;
using WebLog2SQL.Migrations;

namespace WebLog2SQL
{
    public class WebLogDB : DbContext
    {
        public WebLogDB() : this(Program.Settings.ConnectionString) { }
        public WebLogDB(string connectionString)
            : base(connectionString)
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<WebLogDB, Configuration>());
        }
        public DbSet<File> Files { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Property> Properties { get; set; }
    }
}