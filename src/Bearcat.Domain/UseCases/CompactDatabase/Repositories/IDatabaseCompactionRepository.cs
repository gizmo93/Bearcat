namespace Bearcat.Domain.UseCases.CompactDatabase.Repositories;

public interface IDatabaseCompactionRepository
{
    Task CompactDatabaseWithManyFreePagesAsync(CancellationToken cancellationToken);
}
