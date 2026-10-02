using Bearcat.Abstractions.Configurations;
using Bearcat.Infrastructure.Database;
using NUnit.Framework;

namespace Bearcat.IntegrationTest.Utils;

[TestFixtureSource(typeof(BearcatIntegrationTest), nameof(DatabaseProviders))]
public abstract class BearcatIntegrationTest(DatabaseProvider databaseProvider)
{
    private readonly List<BearcatDbContext> dbContexts = [];

    public static IReadOnlyList<DatabaseProvider> DatabaseProviders { get; } =
    [DatabaseProvider.Postgres, DatabaseProvider.Sqlite];

    protected DatabaseProvider DatabaseProvider { get; } = databaseProvider;

    protected BearcatIntegrationTestDatabase Database { get; private set; } = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        Database = await BearcatIntegrationTestDatabase.CreateAsync(DatabaseProvider);
    }

    [TearDown]
    public async Task DisposeDatabaseAsync()
    {
        foreach (var dbContext in dbContexts)
        {
            await dbContext.DisposeAsync();
        }

        dbContexts.Clear();
        await Database.DisposeAsync();
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

        public decimal? GetValue<TConfiguration>(
            System.Linq.Expressions.Expression<Func<TConfiguration, decimal?>> propertySelector
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
