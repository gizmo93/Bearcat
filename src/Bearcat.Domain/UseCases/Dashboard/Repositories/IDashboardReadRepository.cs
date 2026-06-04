using Bearcat.Domain.UseCases.Dashboard.ReadModels;

namespace Bearcat.Domain.UseCases.Dashboard.Repositories;

public interface IDashboardReadRepository
{
    Task<IReadOnlyList<UploadDayReadModel>> GetUploadsPerDayAsync(
        DateOnly? uploadedFrom = null,
        DateOnly? uploadedTo = null,
        CancellationToken cancellationToken = default
    );
}
