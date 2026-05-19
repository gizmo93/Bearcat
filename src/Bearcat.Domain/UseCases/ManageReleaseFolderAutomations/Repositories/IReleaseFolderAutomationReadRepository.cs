using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Dto;

namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Repositories;

public interface IReleaseFolderAutomationReadRepository
{
    Task<IReadOnlyList<ReleaseFolderAutomationDto>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
