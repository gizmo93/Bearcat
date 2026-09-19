using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;

public record ParsedReleaseName
{
    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public int? Season { get; init; }

    public int? Episode { get; init; }

    public int? EpisodeEnd { get; init; }

    public string? Language { get; init; }

    public bool IsMultiLanguage { get; init; }

    public ReleaseResolution Resolution { get; init; }

    public ReleaseSource Source { get; init; }

    public ReleasePlatform Platform { get; init; }

    public bool HasGameMarkers { get; init; }

    public string? Group { get; init; }

    public bool LooksLikeReleaseName =>
        Group is not null
        && (
            Year is not null
            || Language is not null
            || Resolution != ReleaseResolution.Unknown
            || Source != ReleaseSource.Unknown
            || Season is not null
            || Episode is not null
        );
}
