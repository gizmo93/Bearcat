using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;

public class ReleaseInfoResolutionService(
    IReleaseInfoRepository repository,
    INfoDatabaseFactory nfoDatabaseFactory,
    ReleaseNfoResolver nfoResolver,
    ReleaseInfoResolver releaseInfoResolver,
    ReleaseMetadataResolver metadataResolver,
    ReleaseClassificationService classificationService,
    ILogger<ReleaseInfoResolutionService> logger,
    TimeProvider timeProvider
)
{
    private const int MissingReleaseBatchSize = 50;
    private IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel>? activeNfoDatabaseRegistrations;
    private readonly TimeSpan lastCheckedThreshold = TimeSpan.FromDays(1);

    public async Task<int> ProcessMissingReleaseInfosAsync(
        CancellationToken cancellationToken = default
    )
    {
        var hasReleasesWithoutInfos = true;
        var totalResolvedCount = 0;
        var seenReleaseIds = new HashSet<int>();

        var registrations = await GetActiveNfoDatabaseRegistrationsAsync(cancellationToken);

        while (hasReleasesWithoutInfos)
        {
            var (hadReleasesWithoutInfos, resolvedCount) = await ResolveBatchAsync(
                registrations: registrations,
                seenReleaseIds: seenReleaseIds,
                cancellationToken: cancellationToken
            );
            hasReleasesWithoutInfos = hadReleasesWithoutInfos;
            totalResolvedCount += resolvedCount;
        }

        return totalResolvedCount;
    }

    public async Task<bool> TryResolveAsync(
        Release release,
        CancellationToken cancellationToken = default
    )
    {
        var registrations = await GetActiveNfoDatabaseRegistrationsAsync(cancellationToken);

        return await TryResolveAsync(
            release: release,
            registrations: registrations,
            respectLastCheckedAt: false,
            cancellationToken: cancellationToken
        );
    }

    public async Task<bool> ResolveAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var registrations = await GetActiveNfoDatabaseRegistrationsAsync(cancellationToken);

        var release = await repository.GetReleaseWithInfoAsync(releaseId, cancellationToken);

        var resolved = await TryResolveAsync(
            release: release,
            registrations: registrations,
            respectLastCheckedAt: false,
            cancellationToken: cancellationToken
        );

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateReleaseDataException(exception))
        {
            repository.DetachPendingReleaseInfo(release);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                exception,
                "Release info for release {ReleaseName} was already resolved by another worker",
                release.Name
            );

            return false;
        }

        return resolved;
    }

    public async Task<bool> RefreshMetadataAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await repository.GetReleaseWithInfoAsync(releaseId, cancellationToken);
        var resolved = await metadataResolver.TryResolveAndAttachMetadataAsync(
            release,
            cancellationToken
        );

        release.MetadataCheckedAt = timeProvider.GetLocalNow();

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateReleaseDataException(exception))
        {
            repository.DetachPendingReleaseInfo(release);
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                exception,
                "Metadata for release {ReleaseName} was already resolved by another worker",
                release.Name
            );

            return false;
        }

        return resolved;
    }

    private async Task<bool> TryResolveAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        bool respectLastCheckedAt,
        CancellationToken cancellationToken = default
    )
    {
        var lastCheckedThresholdDate = timeProvider.GetLocalNow() - lastCheckedThreshold;
        var releaseInfoNeedsResolution =
            (release.ReleaseInfo is null || release.ReleaseNfo is null)
            && (
                !respectLastCheckedAt
                || release.ReleaseInfoCheckedAt is null
                || release.ReleaseInfoCheckedAt < lastCheckedThresholdDate
            );
        var metadataNeedsResolution =
            metadataResolver.NeedsResolution(release)
            && (
                !respectLastCheckedAt
                || release.MetadataCheckedAt is null
                || release.MetadataCheckedAt < lastCheckedThresholdDate
            );

        var nfoAttached =
            releaseInfoNeedsResolution
            && await nfoResolver.TryResolveAndAttachNfoAsync(
                release: release,
                registrations: registrations,
                cancellationToken: cancellationToken
            );

        var releaseInfoAttached =
            releaseInfoNeedsResolution
            && await releaseInfoResolver.TryResolveAndAttachReleaseInfoAsync(
                release: release,
                registrations: registrations,
                cancellationToken: cancellationToken
            );

        var metadataAttached =
            metadataNeedsResolution
            && await metadataResolver.TryResolveAndAttachMetadataAsync(release, cancellationToken);

        if (nfoAttached || releaseInfoAttached)
        {
            classificationService.Classify(release);
        }

        if (releaseInfoNeedsResolution)
        {
            release.ReleaseInfoCheckedAt = timeProvider.GetLocalNow();
        }

        if (metadataNeedsResolution)
        {
            release.MetadataCheckedAt = timeProvider.GetLocalNow();
        }

        return releaseInfoAttached || nfoAttached || metadataAttached;
    }

    private async Task<(bool FoundReleasesWithoutInfo, int ResolvedCount)> ResolveBatchAsync(
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        HashSet<int> seenReleaseIds,
        CancellationToken cancellationToken
    )
    {
        var lastCheckedThresholdDate = timeProvider.GetLocalNow() - lastCheckedThreshold;

        var releases = await repository.GetReleasesWithoutInfoAsync(
            count: MissingReleaseBatchSize,
            lastCheckedThreshold: lastCheckedThresholdDate,
            excludedReleaseIds: seenReleaseIds,
            cancellationToken: cancellationToken
        );

        var resolvedCount = 0;

        foreach (var release in releases)
        {
            cancellationToken.ThrowIfCancellationRequested();

            seenReleaseIds.Add(release.Id);

            var resolved = await TryResolveAsync(
                release: release,
                registrations: registrations,
                respectLastCheckedAt: true,
                cancellationToken: cancellationToken
            );

            if (resolved)
            {
                resolvedCount++;
            }

            try
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateReleaseDataException(exception))
            {
                repository.DetachPendingReleaseInfo(release);
                await repository.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    exception,
                    "Release info for release {ReleaseName} was already resolved by another worker",
                    release.Name
                );
            }
        }

        return (FoundReleasesWithoutInfo: releases.Count > 0, ResolvedCount: resolvedCount);
    }

    private async Task<
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel>
    > GetActiveNfoDatabaseRegistrationsAsync(CancellationToken cancellationToken)
    {
        activeNfoDatabaseRegistrations ??= (
            await repository.GetActiveNfoDatabaseRegistrationsAsync(cancellationToken)
        )
            .OrderBy(registration =>
                nfoDatabaseFactory.Get(registration.NfoDatabaseClassName).ResolutionPriority
            )
            .ThenBy(registration => registration.NfoDatabaseClassName)
            .ToList();

        return activeNfoDatabaseRegistrations;
    }

    private static bool IsDuplicateReleaseDataException(DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_ReleaseInfos_ReleaseId"
                    or "IX_ReleaseMetadata_ReleaseId"
                    or "IX_ReleaseClassifications_ReleaseId",
            };
    }
}
