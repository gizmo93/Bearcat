using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseClassification
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int? Year { get; set; }

    public int? Season { get; set; }

    public int? Episode { get; set; }

    public int? EpisodeEnd { get; set; }

    public ReleaseContentType ContentType { get; set; }

    public ClassificationSource ContentTypeSource { get; set; }

    public ReleasePlatform Platform { get; set; }

    public ClassificationSource PlatformSource { get; set; }

    public ReleaseResolution Resolution { get; set; }

    public ClassificationSource ResolutionSource { get; set; }

    public ReleaseSource Source { get; set; }

    public ClassificationSource SourceSource { get; set; }

    public string? ReleaseGroupToken { get; set; }

    public string? PrimaryLanguage { get; set; }

    public ClassificationSource LanguageSource { get; set; }

    public bool IsMultiLanguage { get; set; }

    public int ParserVersion { get; set; }

    public DateTime ClassifiedAt { get; set; }
}
