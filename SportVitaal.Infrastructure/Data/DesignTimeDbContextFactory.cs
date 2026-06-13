using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SportVitaal.Infrastructure.Data
{
    // Provides a design-time factory for EF tools so migrations can be created without
    // relying on the startup project's configuration.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Design-time connection for migration scaffolding. The server version is pinned
            // (rather than AutoDetect) so `dotnet ef migrations add` works offline, without
            // opening a connection to a running database.
            var cs = "Server=localhost;Port=3306;Database=SportVitaal;User=root;Password=;";
            var serverVersion = new MariaDbServerVersion(new Version(11, 8, 6));
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(cs, serverVersion)
                .Options;

            return new AppDbContext(options);
        }
    }
}

