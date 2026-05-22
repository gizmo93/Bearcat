using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using DomainReleaseInfo = Bearcat.Domain.Entities.ReleaseInfo;
using NfoReleaseInfo = Bearcat.Abstractions.NfoDatabase.ReleaseInfo;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ReleaseInfoResolutionService(
    IReleaseInfoRepository repository,
    INfoDatabaseFactory nfoDatabaseFactory,
    ILogger<ReleaseInfoResolutionService> logger
)
{
    private const int MissingReleaseBatchSize = 50;
    private static readonly SemaphoreSlim ResolutionLock = new(1, 1);
    private IReadOnlyList<ActiveNfoDatabaseRegistrationReadModel>? activeNfoDatabaseRegistrations;

    public async Task<int> ProcessMissingReleaseInfosAsync(
        CancellationToken cancellationToken = default
    )
    {
        await ResolutionLock.WaitAsync(cancellationToken);
        try
        {
            var releases = await repository.GetReleasesWithoutInfoAsync(
                MissingReleaseBatchSize,
                cancellationToken
            );

            var resolvedCount = 0;
            foreach (var release in releases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (await TryResolveAsync(release, cancellationToken))
                {
                    try
                    {
                        await repository.SaveChangesAsync(cancellationToken);
                        resolvedCount++;
                    }
                    catch (DbUpdateException exception)
                        when (IsDuplicateReleaseInfoException(exception))
                    {
                        repository.DetachPendingReleaseInfos(release);
                        logger.LogInformation(
                            "Release info for release {ReleaseName} was already resolved by another worker",
                            release.Name
                        );
                    }
                }
            }

            return resolvedCount;
        }
        finally
        {
            ResolutionLock.Release();
        }
    }

    public async Task<bool> TryResolveAndSaveAsync(
        Release release,
        CancellationToken cancellationToken = default
    )
    {
        await ResolutionLock.WaitAsync(cancellationToken);
        try
        {
            if (!await TryResolveAsync(release, cancellationToken))
            {
                return false;
            }

            try
            {
                await repository.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsDuplicateReleaseInfoException(exception))
            {
                repository.DetachPendingReleaseInfos(release);
                logger.LogInformation(
                    "Release info for release {ReleaseName} was already resolved by another worker",
                    release.Name
                );
                return false;
            }
        }
        finally
        {
            ResolutionLock.Release();
        }
    }

    private async Task<bool> TryResolveAsync(
        Release release,
        CancellationToken cancellationToken = default
    )
    {
        if (release.ReleaseInfos.Count > 0)
        {
            return false;
        }

        var registrations = await GetActiveNfoDatabaseRegistrationsAsync(cancellationToken);

        foreach (var registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (
                    release.Id > 0
                    && await repository.HasReleaseInfoAsync(
                        release.Id,
                        registration.NfoDatabaseClassName,
                        cancellationToken
                    )
                )
                {
                    return false;
                }

                var nfoDatabase = nfoDatabaseFactory.Get(registration.NfoDatabaseClassName);
                var config = nfoDatabase.DeserializeConfig(registration.SerializedConfig);
                var releaseInfo = await nfoDatabase.GetReleaseInfoAsync(
                    config,
                    release.Name,
                    cancellationToken
                );

                if (releaseInfo is null)
                {
                    continue;
                }

                release.ReleaseInfos.Add(ToEntity(registration.NfoDatabaseClassName, releaseInfo));
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
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve release info for release {ReleaseName} using {NfoDatabase}",
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
        activeNfoDatabaseRegistrations ??= await repository.GetActiveNfoDatabaseRegistrationsAsync(
            cancellationToken
        );

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
