using System;
using System.Data.Entity.Migrations;
using System.Linq;
using NLog;

namespace WebLog2SQL.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<WebLogDB>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public Configuration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;
        }

        protected override void Seed(WebLogDB context)
        {
            if (context.Locations.Any()) return;
            Logger.Info("Initializing Locations table with some suggestions.");
            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            context.Locations.AddRange(new[]{
                new Location { Name = "IIS Local", Root = @"C:\inetpub\logs\LogFiles", DaysToKeep = 7 },
                new Location { Name = "IIS Express", Root = myDocuments + @"\IISExpress\Logs", DaysToKeep = 7 },
                new Location { Name = "TAN", Root = @"\\TAN\C$\inetpub\logs\LogFiles", DaysToKeep = 7 },
                new Location { Name = "DILITHIUM", Root = @"\\DILITHIUM2\Web\Logfiles", DaysToKeep = 7 },
                new Location { Name = "KRYPTONITE", Root = @"\\KRYPTONITE\d$\iis_logfiles", DaysToKeep = 7 }
            });
        }
    }
}
