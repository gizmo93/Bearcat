using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.InversionOfControl;
using Bearcat.Website.ScopedOperations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Bearcat.Website.UnitTest.ScopedOperations;

public class ScopedOperationRunnerTest
{
    [Test]
    public async Task RunAsyncSharesOneDbContextWithinAnOperationAndNotBetweenOperations()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] =
                        "Host=localhost;Database=bearcat;Username=bearcat;Password=bearcat",
                }
            )
            .Build();

        services.AddDatabase(configuration);
        services.AddScoped<FirstWriteRepository>();
        services.AddScoped<SecondWriteRepository>();
        services.AddScoped<WriteService>();
        services.AddSingleton<IScopedOperationRunner, ScopedOperationRunner>();

        await using var serviceProvider = services.BuildServiceProvider();
        var runner = serviceProvider.GetRequiredService<IScopedOperationRunner>();

        var first = await runner.RunAsync(
            (WriteService service) => Task.FromResult(service.Contexts)
        );
        var second = await runner.RunAsync(
            (WriteService service) => Task.FromResult(service.Contexts)
        );

        first.First.ShouldBeSameAs(first.Second);
        second.First.ShouldBeSameAs(second.Second);
        second.First.ShouldNotBeSameAs(first.First);
    }

    private sealed class WriteService(
        FirstWriteRepository firstRepository,
        SecondWriteRepository secondRepository
    )
    {
        public (IBearcatWriteDbContext First, IBearcatWriteDbContext Second) Contexts =>
            (firstRepository.Context, secondRepository.Context);
    }

    private sealed class FirstWriteRepository(IBearcatWriteDbContext context)
    {
        public IBearcatWriteDbContext Context { get; } = context;
    }

    private sealed class SecondWriteRepository(IBearcatWriteDbContext context)
    {
        public IBearcatWriteDbContext Context { get; } = context;
    }
}
