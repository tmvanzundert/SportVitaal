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
            // Default design-time connection to local MariaDB for migrations generation.
            var cs = "Server=localhost;Port=3306;Database=SportVitaal;User=root;Password=;";
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseMySql(cs, ServerVersion.AutoDetect(cs))
                .Options;

            return new AppDbContext(options);
        }
    }
}

