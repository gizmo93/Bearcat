using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using DomainReleaseInfo = Bearcat.Domain.Entities.ReleaseInfo;
using DomainReleaseNfo = Bearcat.Domain.Entities.ReleaseNfo;
using NfoReleaseInfo = Bearcat.Abstractions.NfoDatabase.ReleaseInfo;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseInfoResolutionService(
    IReleaseInfoRepository repository,
    INfoDatabaseFactory nfoDatabaseFactory,
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

        if (registrations.Count == 0)
        {
            return 0;
        }

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
            cancellationToken: cancellationToken
        );
    }

    private async Task<bool> TryResolveAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken = default
    )
    {
        var releaseInfosAttached = await TryResolveAndAttachReleaseInfosAsync(
            release: release,
            registrations: registrations,
            cancellationToken: cancellationToken
        );

        var nfoAttached = await TryResolveAndAttachNfoAsync(
            release: release,
            registrations: registrations,
            cancellationToken: cancellationToken
        );

        release.ReleaseInfosCheckedAt = timeProvider.GetLocalNow();

        return releaseInfosAttached || nfoAttached;
    }

    private async Task<bool> TryResolveAndAttachNfoAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        foreach (var releaseInfo in release.ReleaseInfos.Where(info => info.ReleaseNfo is null))
        {
            if (
                await TryResolveNfoAsync(
                    release: release,
                    releaseInfo: releaseInfo,
                    registrations: registrations,
                    cancellationToken: cancellationToken
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryResolveAndAttachReleaseInfosAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (
                    release.Id > 0
                    && await repository.HasReleaseInfoAsync(
                        releaseId: release.Id,
                        nfoDatabaseClassName: registration.NfoDatabaseClassName,
                        cancellationToken: cancellationToken
                    )
                )
                {
                    return false;
                }

                var nfoDatabase = nfoDatabaseFactory.Get(registration.NfoDatabaseClassName);

                var config = nfoDatabase.DeserializeConfig(registration.SerializedConfig);

                var releaseInfo = await nfoDatabase.GetReleaseInfoAsync(
                    config: config,
                    dirname: release.Name,
                    cancellationToken: cancellationToken
                );

                if (releaseInfo is null)
                {
                    continue;
                }

                var releaseInfoEntity = ToEntity(registration.NfoDatabaseClassName, releaseInfo);

                release.ReleaseInfos.Add(releaseInfoEntity);

                logger.LogInformation(
                    "Resolved release info for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                return true;
            }
            catch (NfoDatabaseRateLimitExceededException exception)
            {
                logger.LogWarning(
                    exception,
                    "Rate limit reached while resolving release info for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                return false;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve release info for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                return false;
            }
        }

        return false;
    }

    private async Task<(bool FoundReleasesWithoutInfo, int ResolvedCount)> ResolveBatchAsync(
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        HashSet<int> seenReleaseIds,
        CancellationToken cancellationToken
    )
    {
        var lastCheckedThresholdDate = DateTime.UtcNow - lastCheckedThreshold;

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
            catch (DbUpdateException exception) when (IsDuplicateReleaseInfoException(exception))
            {
                repository.DetachPendingReleaseInfos(release);
                logger.LogInformation(
                    "Release info for release {ReleaseName} was already resolved by another worker",
                    release.Name
                );
            }
        }

        return (FoundReleasesWithoutInfo: releases.Count > 0, ResolvedCount: resolvedCount);
    }

    private async Task<bool> TryResolveNfoAsync(
        Release release,
        DomainReleaseInfo releaseInfo,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        var localNfo = await ReleaseNfoService.GetLocalNfoAsync(release.ReleaseFolderPath);

        if (localNfo is not null)
        {
            releaseInfo.ReleaseNfo = new DomainReleaseNfo
            {
                FileName = localNfo.FileName,
                Content = localNfo.Content,
            };

            return true;
        }

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var nfoDatabase = nfoDatabaseFactory.Get(registration.NfoDatabaseClassName);

                if (nfoDatabase is not INfoProvider nfoProvider)
                {
                    continue;
                }

                var config = nfoDatabase.DeserializeConfig(registration.SerializedConfig);
                var nfo = await nfoProvider.GetReleaseNfoAsync(
                    config: config,
                    dirname: releaseInfo.ReleaseName,
                    cancellationToken: cancellationToken
                );

                if (nfo is null)
                {
                    continue;
                }

                releaseInfo.ReleaseNfo = new DomainReleaseNfo
                {
                    FileName = nfo.FileName,
                    Content = nfo.Content,
                };

                logger.LogInformation(
                    "Resolved NFO for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                return true;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve NFO for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );
            }
        }

        return false;
    }

    private static DomainReleaseInfo ToEntity(
        string nfoDatabaseClassName,
        NfoReleaseInfo releaseInfo
    )
    {
        return new DomainReleaseInfo
        {
            NfoDatabaseClassName = nfoDatabaseClassName,
            ReleaseName = releaseInfo.ReleaseName,
            ReleaseDatabaseUrl = releaseInfo.ReleaseDatabaseUrl,
            SizeNumber = releaseInfo.Size?.Number,
            SizeUnit = releaseInfo.Size?.Unit,
            VideoType = releaseInfo.VideoType,
            AudioType = releaseInfo.AudioType,
            Genre = releaseInfo.Genre,
            Description = releaseInfo.Description,
            CoverUrl = releaseInfo.CoverUrl,
            ExternalInfos = releaseInfo
                .ExternalInfos.Select(externalInfo => new ReleaseExternalInfo
                {
                    Type = externalInfo.Type,
                    Title = externalInfo.Title,
                    Urls = externalInfo
                        .Urls.Select(url => new ReleaseExternalInfoUrl
                        {
                            Type = url.Type,
                            Url = url.Value,
                        })
                        .ToList(),
                })
                .ToList(),
        };
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

    private static bool IsDuplicateReleaseInfoException(DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_ReleaseInfos_ReleaseId_NfoDatabaseClassName",
            };
    }
}
