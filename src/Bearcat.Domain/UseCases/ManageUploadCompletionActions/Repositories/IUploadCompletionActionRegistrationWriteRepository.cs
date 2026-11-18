using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageUploadCompletionActions.Repositories;

public interface IUploadCompletionActionRegistrationWriteRepository
{
    Task<UploadCompletionActionRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    );

    void Add(UploadCompletionActionRegistration registration);

    void Remove(UploadCompletionActionRegistration registration);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
