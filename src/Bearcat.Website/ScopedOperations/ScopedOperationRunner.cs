using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.ScopedOperations;

public sealed class ScopedOperationRunner(IServiceScopeFactory scopeFactory)
    : IScopedOperationRunner
{
    public Task RunAsync<TService>(Func<TService, Task> operation)
        where TService : notnull => RunAsync<TService>((service, _) => operation(service));

    public Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> operation)
        where TService : notnull => RunAsync<TService, TResult>((service, _) => operation(service));

    public async Task RunAsync<TService1, TService2>(Func<TService1, TService2, Task> operation)
        where TService1 : notnull
        where TService2 : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service1 = scope.ServiceProvider.GetRequiredService<TService1>();
        var service2 = scope.ServiceProvider.GetRequiredService<TService2>();
        await operation(service1, service2);
    }

    public async Task<TResult> RunAsync<TService1, TService2, TResult>(
        Func<TService1, TService2, Task<TResult>> operation
    )
        where TService1 : notnull
        where TService2 : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service1 = scope.ServiceProvider.GetRequiredService<TService1>();
        var service2 = scope.ServiceProvider.GetRequiredService<TService2>();
        return await operation(service1, service2);
    }

    public async Task RunAsync<TService1, TService2, TService3>(
        Func<TService1, TService2, TService3, Task> operation
    )
        where TService1 : notnull
        where TService2 : notnull
        where TService3 : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service1 = scope.ServiceProvider.GetRequiredService<TService1>();
        var service2 = scope.ServiceProvider.GetRequiredService<TService2>();
        var service3 = scope.ServiceProvider.GetRequiredService<TService3>();
        await operation(service1, service2, service3);
    }

    public async Task RunAsync<TService>(
        Func<TService, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        await operation(service, cancellationToken);
    }

    public async Task<TResult> RunAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await operation(service, cancellationToken);
    }
}
