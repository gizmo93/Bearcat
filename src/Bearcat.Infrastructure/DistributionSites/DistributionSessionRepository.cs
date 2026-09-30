using System.Text.Json;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.DistributionSites;

public sealed class DistributionSessionRepository(
    IBearcatWriteDbContext dbContext,
    ISecretProtector secretProtector
) : IDistributionSessionRepository
{
    public async Task<DistributionSession?> GetByRegistrationIdAsync(
        int registrationId,
        CancellationToken cancellationToken
    )
    {
        var registration = await dbContext.DistributionSiteRegistrations.FirstOrDefaultAsync(
            entity => entity.Id == registrationId,
            cancellationToken
        );

        if (registration?.EncryptedSession is null)
        {
            return null;
        }

        var json = secretProtector.Unprotect(registration.EncryptedSession);
        var serializedSession = JsonSerializer.Deserialize<SerializedSession>(json);
        if (serializedSession?.BaseUrl is null)
        {
            return null;
        }

        return new DistributionSession(
            BaseUrl: serializedSession.BaseUrl,
            UserAgent: serializedSession.UserAgent,
            Cookies: serializedSession.Cookies
        );
    }

    public async Task SaveAsync(
        int registrationId,
        DistributionSession session,
        CancellationToken cancellationToken
    )
    {
        var registration = await dbContext.DistributionSiteRegistrations.FirstAsync(
            entity => entity.Id == registrationId,
            cancellationToken
        );

        var json = JsonSerializer.Serialize(
            new SerializedSession(
                BaseUrl: session.BaseUrl,
                UserAgent: session.UserAgent,
                Cookies: session.Cookies
            )
        );
        registration.EncryptedSession = secretProtector.Protect(json);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int registrationId, CancellationToken cancellationToken)
    {
        var registration = await dbContext.DistributionSiteRegistrations.FirstOrDefaultAsync(
            entity => entity.Id == registrationId,
            cancellationToken
        );

        if (registration?.EncryptedSession is null)
        {
            return;
        }

        registration.EncryptedSession = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SerializedSession(
        string? BaseUrl,
        string UserAgent,
        IReadOnlyList<SessionCookie> Cookies
    );
}
