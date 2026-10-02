using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bearcat.Infrastructure.Migrations.Postgres;

public class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BearcatDbContext>
{
    public BearcatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseBearcatPostgres(
#pragma warning disable S2068
            "Host=localhost;Database=bearcat;Username=postgres;Password=postgres"
#pragma warning restore S2068
        );

        return new BearcatDbContext(optionsBuilder.Options);
    }
}
