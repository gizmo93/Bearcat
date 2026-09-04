using Bearcat.Abstractions.Configurations;
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

    protected static IApplicationConfigurationProvider CreateNotificationConfigurationProvider() =>
        new TestApplicationConfigurationProvider();

    private sealed class TestApplicationConfigurationProvider : IApplicationConfigurationProvider
    {
        public TConfiguration GetConfiguration<TConfiguration>()
            where TConfiguration : IApplicationConfiguration, new() => new();

        public bool GetValue<TConfiguration>(
            System.Linq.Expressions.Expression<Func<TConfiguration, bool>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new() =>
            propertySelector.Compile()(new());

        public int GetValue<TConfiguration>(
            System.Linq.Expressions.Expression<Func<TConfiguration, int>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new() =>
            propertySelector.Compile()(new());

        public int? GetValue<TConfiguration>(
            System.Linq.Expressions.Expression<Func<TConfiguration, int?>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new() =>
            propertySelector.Compile()(new());

        public string? GetValue<TConfiguration>(
            System.Linq.Expressions.Expression<Func<TConfiguration, string?>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new() =>
            propertySelector.Compile()(new());

        public TValue GetValue<TConfiguration, TValue>(
            System.Linq.Expressions.Expression<Func<TConfiguration, TValue>> propertySelector
        )
            where TConfiguration : IApplicationConfiguration, new() =>
            propertySelector.Compile()(new());
    }
}
