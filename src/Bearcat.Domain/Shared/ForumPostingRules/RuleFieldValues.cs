using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ForumPostingRules;

public sealed record RuleFieldValues(
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
    string? Genre
)
{
    public static RuleFieldValues FromRelease(Release release)
    {
        var classification = release.Classification;

        return new RuleFieldValues(
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
            Genre: release.Metadata?.Genre
        );
    }

    public static RuleFieldValues FromReleaseCollection(ReleaseCollection collection)
    {
        var classifications = collection
            .Releases.Select(release => release.Classification)
            .OfType<ReleaseClassification>()
            .ToList();

        return new RuleFieldValues(
            ReleaseName: collection.Name,
            Resolution: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.Resolution,
                fallbackValue: ReleaseResolution.Unknown
            ),
            PrimaryLanguage: ResolveCollectionLanguage(collection),
            IsMultiLanguage: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.IsMultiLanguage,
                fallbackValue: false
            ),
            ContentType: ResolveCollectionContentType(collection),
            Platform: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.Platform,
                fallbackValue: ReleasePlatform.Unknown
            ),
            Source: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.Source,
                fallbackValue: ReleaseSource.Unknown
            ),
            ReleaseGroupName: collection.ReleaseGroup.Name,
            ReleaseGroupToken: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.ReleaseGroupToken,
                fallbackValue: null
            ),
            Year: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.Year,
                fallbackValue: null
            ),
            Season: GetCommonValueOrFallback(
                classifications,
                selectValue: classification => classification.Season,
                fallbackValue: null
            ),
            Episode: null,
            Genre: GetCommonValueOrFallback(
                collection
                    .Releases.Select(release => release.Metadata?.Genre)
                    .Where(genre => !string.IsNullOrWhiteSpace(genre))
                    .ToList(),
                fallbackValue: null
            )
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

        return GetCommonValueOrFallback(memberContentTypes, ReleaseContentType.Other);
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

        return GetCommonValueOrFallback(memberLanguages, null);
    }

    private static TValue? GetCommonValueOrFallback<TValue>(
        List<ReleaseClassification> classifications,
        Func<ReleaseClassification, TValue> selectValue,
        TValue? fallbackValue
    )
    {
        return GetCommonValueOrFallback(
            classifications.Select(selectValue).ToList(),
            fallbackValue
        );
    }

    private static TValue? GetCommonValueOrFallback<TValue>(
        List<TValue> values,
        TValue? fallbackValue
    )
    {
        var distinctValues = values.Distinct().ToList();

        return distinctValues.Count == 1 ? distinctValues[0] : fallbackValue;
    }
}
