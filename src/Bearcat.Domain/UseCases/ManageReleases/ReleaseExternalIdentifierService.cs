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
        var imdbIds = values
            .SelectMany(ImdbIdParser.ExtractAll)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        release.ExternalIdentifiers.RemoveAll(identifier =>
            identifier.Type == ExternalIdentifierType.Imdb
            && identifier.Source == source
            && !imdbIds.Contains(identifier.Value)
        );

        foreach (var imdbId in imdbIds)
        {
            if (
                release.ExternalIdentifiers.Any(identifier =>
                    identifier.Type == ExternalIdentifierType.Imdb
                    && identifier.Source == source
                    && identifier.Value == imdbId
                )
            )
            {
                continue;
            }

            release.ExternalIdentifiers.Add(
                new ReleaseExternalIdentifier
                {
                    Type = ExternalIdentifierType.Imdb,
                    Value = imdbId,
                    Source = source,
                }
            );
        }
    }
}
