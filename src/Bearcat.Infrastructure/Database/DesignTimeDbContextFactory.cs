using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bearcat.Infrastructure.Database;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BearcatDbContext>
{
    public BearcatDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();

        // Dummy credentials when creating migrations using dotnet-ef
        optionsBuilder.UseNpgsql(
#pragma warning disable S2068
            "Host=localhost;Database=bearcat;Username=postgres;Password=postgres"
#pragma warning restore S2068
        );
        return new BearcatDbContext(optionsBuilder.Options);
    }
}
