using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
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
            cancellationToken: cancellationToken
        );

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateReleaseInfoException(exception))
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

    private async Task<bool> TryResolveAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken = default
    )
    {
        var nfoAttached = await TryResolveAndAttachNfoAsync(
            release: release,
            registrations: registrations,
            cancellationToken: cancellationToken
        );

        var releaseInfoAttached = await TryResolveAndAttachReleaseInfoAsync(
            release: release,
            registrations: registrations,
            cancellationToken: cancellationToken
        );

        release.ReleaseInfoCheckedAt = timeProvider.GetLocalNow();

        return releaseInfoAttached || nfoAttached;
    }

    private async Task<bool> TryResolveAndAttachNfoAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        if (release.ReleaseNfo is not null)
        {
            return false;
        }

        var localNfo = await ReleaseNfoService.GetLocalNfoAsync(release.ReleaseFolderPath);

        if (localNfo is not null)
        {
            AttachNfo(release, localNfo.FileName, localNfo.Content);
            return true;
        }

        return await TryResolveNfoAsync(release, registrations, cancellationToken);
    }

    private async Task<bool> TryResolveAndAttachReleaseInfoAsync(
        Release release,
        IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel> registrations,
        CancellationToken cancellationToken
    )
    {
        var initialIdentifierCount = release.ExternalIdentifiers.Count;
        var releaseInfoAttached = false;

        if (release.ReleaseInfo is not null)
        {
            ReleaseExternalIdentifierService.SyncImdbIds(
                release,
                GetExternalIdentifierSource(release.ReleaseInfo.NfoDatabaseClassName),
                release
                    .ReleaseInfo.ExternalInfos.SelectMany(info => info.Urls)
                    .Select(url => url.Url)
                    .ToList()
            );

            if (
                release.ExternalIdentifiers.Any(identifier =>
                    identifier.Type == ExternalIdentifierType.Imdb
                )
            )
            {
                return release.ExternalIdentifiers.Count != initialIdentifierCount;
            }
        }

        if (
            release.ReleaseInfo is null
            && release.Id > 0
            && await repository.HasReleaseInfoAsync(release.Id, cancellationToken)
        )
        {
            return false;
        }

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
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

                if (release.ReleaseInfo is null)
                {
                    release.ReleaseInfo = ToEntity(registration.NfoDatabaseClassName, releaseInfo);
                    releaseInfoAttached = true;

                    logger.LogInformation(
                        "Resolved release info for release {ReleaseName} using {NfoDatabase}",
                        release.Name,
                        registration.NfoDatabaseClassName
                    );
                }

                ReleaseExternalIdentifierService.SyncImdbIds(
                    release,
                    GetExternalIdentifierSource(registration.NfoDatabaseClassName),
                    releaseInfo
                        .ExternalInfos.SelectMany(info => info.Urls)
                        .Select(url => url.Value)
                        .ToList()
                );

                if (
                    release.ExternalIdentifiers.Any(identifier =>
                        identifier.Type == ExternalIdentifierType.Imdb
                    )
                )
                {
                    return true;
                }
            }
            catch (NfoDatabaseRateLimitExceededException exception)
            {
                logger.LogWarning(
                    exception,
                    "Rate limit reached while resolving release info for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                continue;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve release info for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                continue;
            }
        }

        return releaseInfoAttached || release.ExternalIdentifiers.Count != initialIdentifierCount;
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

    private async Task<bool> TryResolveNfoAsync(
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
                var nfoDatabase = nfoDatabaseFactory.Get(registration.NfoDatabaseClassName);

                if (nfoDatabase is not INfoProvider nfoProvider)
                {
                    continue;
                }

                var config = nfoDatabase.DeserializeConfig(registration.SerializedConfig);
                var nfo = await nfoProvider.GetReleaseNfoAsync(
                    config: config,
                    dirname: release.ReleaseInfo?.ReleaseName ?? release.Name,
                    cancellationToken: cancellationToken
                );

                if (nfo is null)
                {
                    continue;
                }

                AttachNfo(release, nfo.FileName, nfo.Content);

                logger.LogInformation(
                    "Resolved NFO for release {ReleaseName} using {NfoDatabase}",
                    release.Name,
                    registration.NfoDatabaseClassName
                );

                await SaveNfoFileToDiskAsync(
                    release: release,
                    fileName: nfo.FileName,
                    content: nfo.Content,
                    cancellationToken: cancellationToken
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

    private static void AttachNfo(Release release, string fileName, string content)
    {
        release.ReleaseNfo = new DomainReleaseNfo { FileName = fileName, Content = content };
        ReleaseExternalIdentifierService.SyncImdbIds(
            release,
            ExternalIdentifierSource.Nfo,
            [content]
        );
    }

    private static ExternalIdentifierSource GetExternalIdentifierSource(string className)
    {
        return className.Contains("Srrdb", StringComparison.OrdinalIgnoreCase)
            ? ExternalIdentifierSource.Srrdb
            : ExternalIdentifierSource.Xrel;
    }

    private async Task SaveNfoFileToDiskAsync(
        Release release,
        string fileName,
        string content,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await ReleaseNfoService.SaveNfoFileAsync(
                releaseFolderPath: release.ReleaseFolderPath,
                fileName: fileName,
                releaseName: release.Name,
                content: content,
                cancellationToken: cancellationToken
            );

            if (result is ReleaseNfoFileSaveResult.Saved)
            {
                logger.LogInformation("Saved NFO file for release {ReleaseName}", release.Name);
            }
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Failed to save NFO file for release {ReleaseName}",
                release.Name
            );
        }
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
                ConstraintName: "IX_ReleaseInfos_ReleaseId",
            };
    }
}
