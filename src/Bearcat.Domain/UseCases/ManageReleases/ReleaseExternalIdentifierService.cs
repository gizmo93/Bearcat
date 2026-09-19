using Bearcat.Domain.Entities;
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
            extractor: Parsers.ImdbIdParser.ExtractAll
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
            extractor: Parsers.SteamAppIdParser.ExtractAll
        );
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
