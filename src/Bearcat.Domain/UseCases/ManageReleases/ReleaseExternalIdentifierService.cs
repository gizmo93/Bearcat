using Bearcat.Abstractions.MediaMetadataDatabase;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageReleases.Parsers;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases;

public static class ReleaseExternalIdentifierService
{
    public static void SyncImdbIds(
        Release release,
        ExternalIdentifierSource source,
        IReadOnlyList<string?> values
    )
    {
        Sync(
            release: release,
            type: ExternalIdentifierType.Imdb,
            source: source,
            values: values,
            extractor: ImdbIdParser.ExtractAll
        );
    }

    public static void SyncSteamAppIds(
        Release release,
        ExternalIdentifierSource source,
        IReadOnlyList<string?> values
    )
    {
        Sync(
            release: release,
            type: ExternalIdentifierType.Steam,
            source: source,
            values: values,
            extractor: SteamAppIdParser.ExtractAll
        );
    }

    public static void SyncIdentifierValue(
        Release release,
        ExternalIdentifierType type,
        ExternalIdentifierSource source,
        string? value
    )
    {
        Sync(
            release: release,
            type: type,
            source: source,
            values: [value],
            extractor: item => string.IsNullOrWhiteSpace(item) ? [] : [item]
        );
    }

    public static void StoreIdentifiersReportedByMediaDatabase(
        Release release,
        MediaMetadata metadata
    )
    {
        if (metadata.TmdbId is not null)
        {
            SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Tmdb,
                source: ExternalIdentifierSource.Tmdb,
                value: metadata.TmdbId
            );

            SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Imdb,
                source: ExternalIdentifierSource.Tmdb,
                value: metadata.ImdbId
            );
        }

        if (metadata.TvdbId is not null)
        {
            SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Tvdb,
                source: ExternalIdentifierSource.Tvdb,
                value: metadata.TvdbId
            );

            SyncIdentifierValue(
                release: release,
                type: ExternalIdentifierType.Imdb,
                source: ExternalIdentifierSource.Tvdb,
                value: metadata.ImdbId
            );
        }
    }

    public static string? FindValueWithHighestSourcePriority(
        IReadOnlyList<ReleaseExternalIdentifier> identifiers,
        ExternalIdentifierType type
    )
    {
        return identifiers
            .Where(identifier => identifier.Type == type)
            .OrderBy(identifier => GetSourcePriority(identifier.Source))
            .Select(identifier => identifier.Value)
            .FirstOrDefault();
    }

    public static ReleaseExternalIdentifierReadModel? FindIdentifierWithHighestSourcePriority(
        IReadOnlyList<ReleaseExternalIdentifierReadModel> identifiers,
        ExternalIdentifierType type
    )
    {
        return identifiers
            .Where(identifier => identifier.Type == type)
            .OrderBy(identifier => identifier.Sources.Min(GetSourcePriority))
            .FirstOrDefault();
    }

    private static int GetSourcePriority(ExternalIdentifierSource source)
    {
        return source switch
        {
            ExternalIdentifierSource.Manual => 1,
            ExternalIdentifierSource.Nfo => 2,
            ExternalIdentifierSource.Xrel => 3,
            ExternalIdentifierSource.Srrdb => 4,
            ExternalIdentifierSource.Tmdb => 5,
            ExternalIdentifierSource.Tvdb => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
    }

    private static void Sync(
        Release release,
        ExternalIdentifierType type,
        ExternalIdentifierSource source,
        IReadOnlyList<string?> values,
        Func<string?, IReadOnlyList<string>> extractor
    )
    {
        var extractedValues = values
            .SelectMany(extractor)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        release.ExternalIdentifiers.RemoveAll(identifier =>
            identifier.Type == type
            && identifier.Source == source
            && !extractedValues.Contains(identifier.Value)
        );

        foreach (var extractedValue in extractedValues)
        {
            if (
                release.ExternalIdentifiers.Any(identifier =>
                    identifier.Type == type
                    && identifier.Source == source
                    && identifier.Value == extractedValue
                )
            )
            {
                continue;
            }

            release.ExternalIdentifiers.Add(
                new ReleaseExternalIdentifier
                {
                    Type = type,
                    Value = extractedValue,
                    Source = source,
                }
            );
        }
    }
}
