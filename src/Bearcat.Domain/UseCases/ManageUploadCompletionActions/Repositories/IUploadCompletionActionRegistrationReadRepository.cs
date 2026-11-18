using Bearcat.Domain.UseCases.ManageUploadCompletionActions.ReadModels;

namespace Bearcat.Domain.UseCases.ManageUploadCompletionActions.Repositories;

public interface IUploadCompletionActionRegistrationReadRepository
{
    Task<IReadOnlyList<UploadCompletionActionRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    );
}
