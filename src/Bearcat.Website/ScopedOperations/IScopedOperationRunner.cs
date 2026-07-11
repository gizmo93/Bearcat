namespace Bearcat.Website.ScopedOperations;

public interface IScopedOperationRunner
{
    TResult Run<TService, TResult>(Func<TService, TResult> operation)
        where TService : notnull;

    Task RunAsync<TService>(Func<TService, Task> operation)
        where TService : notnull;

    Task<TResult> RunAsync<TService, TResult>(Func<TService, Task<TResult>> operation)
        where TService : notnull;

    Task RunAsync<TService1, TService2>(Func<TService1, TService2, Task> operation)
        where TService1 : notnull
        where TService2 : notnull;

    Task<TResult> RunAsync<TService1, TService2, TResult>(
        Func<TService1, TService2, Task<TResult>> operation
    )
        where TService1 : notnull
        where TService2 : notnull;

    Task RunAsync<TService1, TService2, TService3>(
        Func<TService1, TService2, TService3, Task> operation
    )
        where TService1 : notnull
        where TService2 : notnull
        where TService3 : notnull;

    Task RunAsync<TService>(
        Func<TService, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull;

    Task<TResult> RunAsync<TService, TResult>(
        Func<TService, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default
    )
        where TService : notnull;
}
