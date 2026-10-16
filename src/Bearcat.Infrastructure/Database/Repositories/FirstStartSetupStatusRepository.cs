using Bearcat.Domain.UseCases.FirstStartSetup.Repositories;
using Bearcat.Domain.UseCases.FirstStartSetup.Status;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.Database.Repositories;

public class FirstStartSetupStatusRepository(IBearcatReadDbContext dbRead)
    : IFirstStartSetupStatusRepository
{
    private const int HosterRegistrationMarker = 1;
    private const int ReleaseTemplateMarker = 2;
    private const int ReleaseFolderAutomationMarker = 3;
    private const int RemoteSourceAutomationMarker = 4;
    private const int ReleaseMarker = 5;

    public async Task<FirstStartSetupDatabaseStatus> GetDatabaseStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var existingEntityMarkers = await dbRead
            .HosterRegistrations.Select(hosterRegistration => HosterRegistrationMarker)
            .Take(1)
            .Concat(
                dbRead.ReleaseTemplates.Select(releaseTemplate => ReleaseTemplateMarker).Take(1)
            )
            .Concat(
                dbRead
                    .ReleaseFolderAutomations.Select(automation => ReleaseFolderAutomationMarker)
                    .Take(1)
            )
            .Concat(
                dbRead
                    .RemoteSourceAutomations.Select(automation => RemoteSourceAutomationMarker)
                    .Take(1)
            )
            .Concat(dbRead.Releases.Select(release => ReleaseMarker).Take(1))
            .ToListAsync(cancellationToken);

        return new FirstStartSetupDatabaseStatus(
            HasHosterRegistration: existingEntityMarkers.Contains(HosterRegistrationMarker),
            HasReleaseTemplate: existingEntityMarkers.Contains(ReleaseTemplateMarker),
            HasReleaseFolderAutomationOrRemoteSourceAutomation: existingEntityMarkers.Contains(
                ReleaseFolderAutomationMarker
            ) || existingEntityMarkers.Contains(RemoteSourceAutomationMarker),
            HasRelease: existingEntityMarkers.Contains(ReleaseMarker)
        );
    }
}
