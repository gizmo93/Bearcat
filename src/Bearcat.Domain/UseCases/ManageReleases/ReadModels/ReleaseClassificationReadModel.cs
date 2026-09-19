using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseClassificationReadModel(
    string Title,
    int? Year,
    int? Season,
    int? Episode,
    int? EpisodeEnd,
    ReleaseContentType ContentType,
    ClassificationSource ContentTypeSource,
    ReleasePlatform Platform,
    ClassificationSource PlatformSource,
    ReleaseResolution Resolution,
    ClassificationSource ResolutionSource,
    ReleaseSource Source,
    ClassificationSource SourceSource,
    string? ReleaseGroupToken,
    string? PrimaryLanguage,
    ClassificationSource LanguageSource,
    bool IsMultiLanguage,
    int ParserVersion,
    DateTime ClassifiedAt
);
