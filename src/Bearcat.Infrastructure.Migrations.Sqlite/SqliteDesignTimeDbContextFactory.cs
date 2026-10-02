using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bearcat.Infrastructure.Migrations.Sqlite;

public class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BearcatDbContext>
{
    public BearcatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseBearcatSqlite(
            DatabaseConfiguration.CreateSqliteConnectionString("bearcat-design-time.db")
        );

        return new BearcatDbContext(optionsBuilder.Options);
    }
}
