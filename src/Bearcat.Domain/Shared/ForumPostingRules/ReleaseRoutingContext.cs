using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed record ReleaseRoutingContext(
    string ReleaseName,
    ReleaseResolution Resolution,
    string? PrimaryLanguage,
    bool IsMultiLanguage,
    ReleaseContentType ContentType,
    ReleasePlatform Platform,
    ReleaseSource Source,
    string? ReleaseGroupName,
    string? ReleaseGroupToken,
    int? Year,
    int? Season,
    int? Episode,
    bool HasClassification
)
{
    public static ReleaseRoutingContext FromRelease(Release release)
    {
        var classification = release.Classification;

        return new ReleaseRoutingContext(
            ReleaseName: release.Name,
            Resolution: classification?.Resolution ?? ReleaseResolution.Unknown,
            PrimaryLanguage: ReleaseLanguageResolver.Resolve(release),
            IsMultiLanguage: classification?.IsMultiLanguage ?? false,
            ContentType: ResolveContentType(release) ?? ReleaseContentType.Other,
            Platform: classification?.Platform ?? ReleasePlatform.Unknown,
            Source: classification?.Source ?? ReleaseSource.Unknown,
            ReleaseGroupName: release.ReleaseGroup?.Name,
            ReleaseGroupToken: classification?.ReleaseGroupToken,
            Year: classification?.Year,
            Season: classification?.Season,
            Episode: classification?.Episode,
            HasClassification: classification is not null
        );
    }

    public static ReleaseRoutingContext FromReleaseCollection(ReleaseCollection collection)
    {
        var classifications = collection
            .Releases.Select(release => release.Classification)
            .OfType<ReleaseClassification>()
            .ToList();

        return new ReleaseRoutingContext(
            ReleaseName: collection.Name,
            Resolution: UniformValueOrDefault(
                classifications.Select(classification => classification.Resolution).ToList(),
                ReleaseResolution.Unknown
            ),
            PrimaryLanguage: ResolveCollectionLanguage(collection),
            IsMultiLanguage: UniformValueOrDefault(
                classifications.Select(classification => classification.IsMultiLanguage).ToList(),
                false
            ),
            ContentType: ResolveCollectionContentType(collection),
            Platform: UniformValueOrDefault(
                classifications.Select(classification => classification.Platform).ToList(),
                ReleasePlatform.Unknown
            ),
            Source: UniformValueOrDefault(
                classifications.Select(classification => classification.Source).ToList(),
                ReleaseSource.Unknown
            ),
            ReleaseGroupName: collection.ReleaseGroup.Name,
            ReleaseGroupToken: UniformValueOrDefault(
                classifications.Select(classification => classification.ReleaseGroupToken).ToList(),
                null
            ),
            Year: UniformValueOrDefault(
                classifications.Select(classification => classification.Year).ToList(),
                null
            ),
            Season: UniformValueOrDefault(
                classifications.Select(classification => classification.Season).ToList(),
                null
            ),
            Episode: null,
            HasClassification: classifications.Count > 0
        );
    }

    private static ReleaseContentType? ResolveContentType(Release release)
    {
        if (Enum.IsDefined(release.ReleaseContentType))
        {
            return release.ReleaseContentType;
        }

        return release.Classification?.ContentType;
    }

    private static ReleaseContentType ResolveCollectionContentType(ReleaseCollection collection)
    {
        if (Enum.IsDefined(collection.ReleaseContentType))
        {
            return collection.ReleaseContentType;
        }

        var memberContentTypes = collection
            .Releases.Select(ResolveContentType)
            .OfType<ReleaseContentType>()
            .ToList();

        return UniformValueOrDefault(memberContentTypes, ReleaseContentType.Other);
    }

    private static string? ResolveCollectionLanguage(ReleaseCollection collection)
    {
        var collectionLanguage = LanguageCatalog.Resolve(collection.PrimaryLanguageCode);

        if (collectionLanguage is not null)
        {
            return collectionLanguage;
        }

        var memberLanguages = collection
            .Releases.Select(ReleaseLanguageResolver.Resolve)
            .OfType<string>()
            .ToList();

        return UniformValueOrDefault(memberLanguages, null);
    }

    private static TValue? UniformValueOrDefault<TValue>(List<TValue> values, TValue? defaultValue)
    {
        var distinctValues = values.Distinct().ToList();

        return distinctValues.Count == 1 ? distinctValues[0] : defaultValue;
    }
}
