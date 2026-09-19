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
            ContentType: ResolveContentType(release, classification),
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

    private static ReleaseContentType ResolveContentType(
        Release release,
        ReleaseClassification? classification
    )
    {
        if (Enum.IsDefined(release.ReleaseContentType))
        {
            return release.ReleaseContentType;
        }

        return classification?.ContentType ?? ReleaseContentType.Other;
    }
}
