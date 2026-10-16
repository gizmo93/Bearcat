using Bearcat.Domain.UseCases.FirstStartSetup.Status;

namespace Bearcat.Domain.UseCases.FirstStartSetup.Repositories;

public interface IFirstStartSetupStatusRepository
{
    Task<FirstStartSetupDatabaseStatus> GetDatabaseStatusAsync(
        CancellationToken cancellationToken = default
    );
}
