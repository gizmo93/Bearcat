using Bearcat.Abstractions.NfoDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using DomainReleaseInfo = Bearcat.Domain.Entities.ReleaseInfo;
using NfoReleaseInfo = Bearcat.Abstractions.NfoDatabase.ReleaseInfo;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseInfoResolution;

public class ReleaseInfoResolver(
    IReleaseInfoRepository repository,
    INfoDatabaseFactory nfoDatabaseFactory,
    ILogger<ReleaseInfoResolver> logger
)
{
    public async Task<bool> TryResolveAndAttachReleaseInfoAsync(
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
                release: release,
                source: GetExternalIdentifierSource(release.ReleaseInfo.NfoDatabaseClassName),
                values: release
                    .ReleaseInfo.ExternalInfos.SelectMany(info => info.Urls)
                    .Select(url => url.Url)
                    .ToList()
            );

            ReleaseExternalIdentifierService.SyncSteamAppIds(
                release: release,
                source: GetExternalIdentifierSource(release.ReleaseInfo.NfoDatabaseClassName),
                values: release
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
                    var metadataTitle = releaseInfo
                        .ExternalInfos.Select(info => info.Title)
                        .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title));

                    if (
                        release.Metadata is null
                        && (
                            !string.IsNullOrWhiteSpace(metadataTitle)
                            || !string.IsNullOrWhiteSpace(releaseInfo.Genre)
                            || !string.IsNullOrWhiteSpace(releaseInfo.Description)
                            || !string.IsNullOrWhiteSpace(releaseInfo.CoverUrl)
                        )
                    )
                    {
                        release.Metadata = new ReleaseMetadata
                        {
                            MetadataDatabaseClassName = registration.NfoDatabaseClassName,
                            Title = metadataTitle ?? releaseInfo.ReleaseName,
                            Genre = releaseInfo.Genre,
                            Description = releaseInfo.Description,
                            CoverUrl = releaseInfo.CoverUrl,
                        };
                    }

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

                ReleaseExternalIdentifierService.SyncSteamAppIds(
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
            ContentKind = releaseInfo.ContentKind,
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

    private static ExternalIdentifierSource GetExternalIdentifierSource(string className)
    {
        return className.Contains("Srrdb", StringComparison.OrdinalIgnoreCase)
            ? ExternalIdentifierSource.Srrdb
            : ExternalIdentifierSource.Xrel;
    }
}
