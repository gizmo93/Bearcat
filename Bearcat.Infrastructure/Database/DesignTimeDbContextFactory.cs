using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bearcat.Infrastructure.Database;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BearcatDbContext>
{
    public BearcatDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=bearcat;Username=postgres;Password=postgres");
        return new BearcatDbContext(optionsBuilder.Options);
    }
}
