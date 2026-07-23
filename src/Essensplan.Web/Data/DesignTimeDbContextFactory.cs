using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Essensplan.Web.Data;

/// <summary>
/// Used only by `dotnet ef migrations add/update` so the CLI can build an AppDbContext
/// without needing a live MySQL connection to auto-detect the server version.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // A fixed version keeps migration generation deterministic and independent of any
        // reachable database. The app itself uses ServerVersion.AutoDetect at runtime.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 34));
        optionsBuilder.UseMySql("Server=localhost;Database=essensplan;User=root;Password=root;", serverVersion);

        return new AppDbContext(optionsBuilder.Options);
    }
}
