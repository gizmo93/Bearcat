using Bearcat.Infrastructure.Database;
using NUnit.Framework;

namespace Bearcat.IntegrationTest.Utils;

[NonParallelizable]
public abstract class BearcatIntegrationTest
{
    private readonly List<BearcatDbContext> dbContexts = [];

    protected BearcatIntegrationTestDatabase Database { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartDatabaseAsync()
    {
        Database = await BearcatIntegrationTestDatabase.GetOrStartAsync();
    }

    [SetUp]
    public async Task ResetDatabaseAsync()
    {
        await Database.ResetAsync();
    }

    [TearDown]
    public async Task DisposeDbContextsAsync()
    {
        foreach (var dbContext in dbContexts)
        {
            await dbContext.DisposeAsync();
        }

        dbContexts.Clear();
    }

    protected BearcatDbContext CreateDbContext()
    {
        var dbContext = Database.CreateDbContext();
        dbContexts.Add(dbContext);

        return dbContext;
    }
}
