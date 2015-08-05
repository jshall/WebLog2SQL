using System.Data.Entity;
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
        public DbSet<Location> Locations { get; set; }
    }
}