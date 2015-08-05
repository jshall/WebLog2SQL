using System.Data.Entity.Migrations;
using System.Linq;

namespace WebLog2SQL.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<WebLogDB>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(WebLogDB context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data. E.g.

            if (!context.Locations.Any())
                context.Locations.AddRange(new[]{
                    new Location { Name = "DILITHIUM", Root = @"\\DILITHIUM2\Web\Logfiles", DaysToKeep = 7 },
                    new Location { Name = "TAN", Root = @"\\TAN\C$\inetpub\logs\LogFiles", DaysToKeep = 7 },
                    new Location { Name = "CARBONITE", Root = @"\\CARBONITE\c$\inetpub\logs\logfiles", DaysToKeep = 7 },
                    new Location { Name = "KRYPTONITE", Root = @"\\KRYPTONITE\d$\iis_logfiles", DaysToKeep = 7 }
                });
        }
    }
}
