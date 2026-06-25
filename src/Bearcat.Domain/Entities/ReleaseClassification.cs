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

    public ReleaseResolution Resolution { get; set; }

    public ClassificationSource ResolutionSource { get; set; }

    public string? PrimaryLanguage { get; set; }

    public ClassificationSource LanguageSource { get; set; }

    public bool IsMultiLanguage { get; set; }

    public int ParserVersion { get; set; }

    public DateTime ClassifiedAt { get; set; }
}
