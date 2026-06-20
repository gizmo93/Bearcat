using System.Text.Json;
using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Bearcat.Infrastructure.DistributionSites;

public sealed class DatabaseDistributionSessionStore(
    IBearcatWriteDbContext dbContext,
    ISecretProtector secretProtector
) : IDistributionSessionStore
{
    public async Task<DistributionSession?> TryGetAsync(
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
        var payload = JsonSerializer.Deserialize<SessionPayload>(json);
        if (payload is null)
        {
            return null;
        }

        return new DistributionSession(payload.UserAgent, payload.Cookies);
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

        var json = JsonSerializer.Serialize(new SessionPayload(session.UserAgent, session.Cookies));
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

    private sealed record SessionPayload(string UserAgent, IReadOnlyList<SessionCookie> Cookies);
}
