using Bearcat.Abstractions.UploadCompletionAction;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadCompletionActions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class UploadCompletionActionRegistrationRepository(
    IBearcatReadDbContext dbRead,
    IBearcatWriteDbContext dbWrite,
    IUploadCompletionActionFactory uploadCompletionActionFactory
)
    : IUploadCompletionActionRegistrationReadRepository,
        IUploadCompletionActionRegistrationWriteRepository
{
    public async Task<IReadOnlyList<UploadCompletionActionRegistrationReadModel>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        var actionNamesByClassName = uploadCompletionActionFactory
            .GetUploadCompletionActions()
            .ToDictionary(action => action.ClassName, action => action.Name);

        var registrations = await dbRead
            .UploadCompletionActionRegistrations.OrderBy(registration => registration.Name)
            .ThenBy(registration => registration.Id)
            .Select(registration => new
            {
                registration.Id,
                registration.Name,
                registration.ActionClassName,
                registration.IsActive,
                registration.RunForInitialUpload,
                registration.RunForReupload,
                registration.HasUnreadableSecrets,
            })
            .ToListAsync(cancellationToken);

        return registrations
            .Select(registration => new UploadCompletionActionRegistrationReadModel(
                Id: registration.Id,
                Name: registration.Name,
                ActionClassName: registration.ActionClassName,
                ActionName: actionNamesByClassName[registration.ActionClassName],
                IsActive: registration.IsActive,
                RunForInitialUpload: registration.RunForInitialUpload,
                RunForReupload: registration.RunForReupload,
                HasUnreadableSecrets: registration.HasUnreadableSecrets
            ))
            .ToList();
    }

    public async Task<UploadCompletionActionRegistration> GetByIdAsync(
        int id,
        CancellationToken cancellationToken
    )
    {
        return await dbWrite.UploadCompletionActionRegistrations.FirstAsync(
            registration => registration.Id == id,
            cancellationToken
        );
    }

    public void Add(UploadCompletionActionRegistration registration)
    {
        dbWrite.UploadCompletionActionRegistrations.Add(registration);
    }

    public void Remove(UploadCompletionActionRegistration registration)
    {
        dbWrite.UploadCompletionActionRegistrations.Remove(registration);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbWrite.SaveChangesAsync(cancellationToken);
    }
}
