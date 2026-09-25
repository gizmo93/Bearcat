using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;

namespace Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Repositories;

public interface IAdditionalArchiveContentWriteRepository
{
    Task<AdditionalArchiveContent> GetByIdAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken
    );

    Task<bool> NameExistsAsync(
        string name,
        int? excludedAdditionalArchiveContentId,
        CancellationToken cancellationToken
    );

    Task<AdditionalArchiveContentUsageReadModel> GetUsageAsync(
        int additionalArchiveContentId,
        CancellationToken cancellationToken
    );

    void Add(AdditionalArchiveContent additionalArchiveContent);

    void Remove(AdditionalArchiveContent additionalArchiveContent);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
