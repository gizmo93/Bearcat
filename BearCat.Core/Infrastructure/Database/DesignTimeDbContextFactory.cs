using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BearCat.Core.Infrastructure.Database;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BearcatDbContext>
{
    public BearcatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BearcatDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=bearcat;Username=postgres;Password=postgres");
        return new BearcatDbContext(optionsBuilder.Options);
    }
}
