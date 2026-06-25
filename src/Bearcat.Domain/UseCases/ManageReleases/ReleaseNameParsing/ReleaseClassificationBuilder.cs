using Bearcat.Abstractions.Media;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReleaseNameParsing;

public static class ReleaseClassificationBuilder
{
    public const int CurrentParserVersion = 1;

    public static ReleaseClassification Build(
        string releaseName,
        IReadOnlyList<ReleaseMediaFile> mediaFiles
    )
    {
        var parsed = ReleaseNameParser.Parse(releaseName);

        var metadata = mediaFiles
            .Select(file => MediaInfoOutputParser.Parse(file.MediaInfoJson))
            .Where(fileMetadata => fileMetadata is not null)
            .Select(fileMetadata => fileMetadata!)
            .ToList();

        var (resolution, resolutionSource) = ResolveResolution(parsed, metadata);
        var (language, languageSource, isMultiLanguage) = ResolveLanguage(parsed, metadata);

        return new ReleaseClassification
        {
            Title = parsed.Title,
            Year = parsed.Year,
            Season = parsed.Season,
            Episode = parsed.Episode,
            Resolution = resolution,
            ResolutionSource = resolutionSource,
            PrimaryLanguage = language,
            LanguageSource = languageSource,
            IsMultiLanguage = isMultiLanguage,
            ParserVersion = CurrentParserVersion,
        };
    }

    private static (ReleaseResolution Resolution, ClassificationSource Source) ResolveResolution(
        ParsedReleaseName parsed,
        List<MediaFileMetadata> metadata
    )
    {
        if (parsed.Resolution != ReleaseResolution.Unknown)
        {
            return (parsed.Resolution, ClassificationSource.ReleaseName);
        }

        var maxHeight = metadata
            .Select(fileMetadata => fileMetadata.VideoStream?.Height ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        return maxHeight > 0
            ? (MapHeight(maxHeight), ClassificationSource.MediaInfo)
            : (ReleaseResolution.Unknown, ClassificationSource.None);
    }

    private static (string? Language, ClassificationSource Source, bool IsMultiLanguage) ResolveLanguage(
        ParsedReleaseName parsed,
        List<MediaFileMetadata> metadata
    )
    {
        if (parsed.Language is not null)
        {
            return (parsed.Language, ClassificationSource.ReleaseName, parsed.IsMultiLanguage);
        }

        var audioStreams = metadata.SelectMany(fileMetadata => fileMetadata.AudioStreams).ToList();

        if (audioStreams.Count == 0)
        {
            return (null, ClassificationSource.None, parsed.IsMultiLanguage);
        }

        var distinctLanguages = audioStreams
            .Select(stream => LanguageCatalog.Resolve(stream.Language))
            .Where(language => language is not null)
            .Select(language => language!)
            .Distinct()
            .ToList();

        var primaryStream =
            audioStreams.FirstOrDefault(stream => stream.IsDefault) ?? audioStreams[0];
        var primaryLanguage = LanguageCatalog.Resolve(primaryStream.Language);

        var isMultiLanguage = parsed.IsMultiLanguage || distinctLanguages.Count > 1;

        return primaryLanguage is not null
            ? (primaryLanguage, ClassificationSource.MediaInfo, isMultiLanguage)
            : (null, ClassificationSource.None, isMultiLanguage);
    }

    private static ReleaseResolution MapHeight(int height)
    {
        return height switch
        {
            >= 1700 => ReleaseResolution.R2160p,
            >= 850 => ReleaseResolution.R1080p,
            >= 650 => ReleaseResolution.R720p,
            >= 540 => ReleaseResolution.R576p,
            _ => ReleaseResolution.R480p,
        };
    }
}
