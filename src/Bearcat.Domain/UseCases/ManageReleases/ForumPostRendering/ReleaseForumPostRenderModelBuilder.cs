using System.Globalization;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public class ReleaseForumPostRenderModelBuilder(
    IReleaseReadRepository releaseReadRepository,
    ReleaseForumPostUploadBuilder uploadBuilder,
    ForumPostImageLinkBuilder imageLinkBuilder
)
{
    public async Task<IReadOnlyDictionary<int, ReleaseForumPostRenderData>> BuildAsync(
        IReadOnlyList<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        var releasesById = await releaseReadRepository.GetReleasesByIdAsync(
            releaseIds,
            cancellationToken
        );

        if (releasesById.Count == 0)
        {
            return new Dictionary<int, ReleaseForumPostRenderData>();
        }

        var existingReleaseIds = releasesById.Keys.ToList();

        var infosByReleaseId = await releaseReadRepository.GetReleaseInfosByReleaseIdAsync(
            existingReleaseIds,
            cancellationToken
        );

        var metadataByReleaseId = await releaseReadRepository.GetReleaseMetadataByReleaseIdAsync(
            existingReleaseIds,
            cancellationToken
        );

        var nfosByReleaseId = await releaseReadRepository.GetReleaseNfosByReleaseIdAsync(
            existingReleaseIds,
            cancellationToken
        );

        var uploadsByReleaseId = await uploadBuilder.BuildByReleaseIdAsync(
            existingReleaseIds,
            cancellationToken
        );

        var allHostersLinkCryptersByReleaseId =
            await uploadBuilder.BuildAllHostersLinkCryptersByReleaseIdAsync(
                existingReleaseIds,
                cancellationToken
            );

        var mediaFilesByReleaseId = await releaseReadRepository.GetMediaFilesByReleaseIdAsync(
            existingReleaseIds,
            cancellationToken
        );

        var externalIdentifiersByReleaseId =
            await releaseReadRepository.GetReleaseExternalIdentifiersByReleaseIdAsync(
                existingReleaseIds,
                cancellationToken
            );

        var classificationsByReleaseId =
            await releaseReadRepository.GetClassificationsByReleaseIdAsync(
                existingReleaseIds,
                cancellationToken
            );

        var imageLinksByReleaseId = await imageLinkBuilder.BuildForReleasesAsync(
            existingReleaseIds,
            cancellationToken
        );

        var renderDataByReleaseId = new Dictionary<int, ReleaseForumPostRenderData>();

        foreach (var release in releasesById.Values)
        {
            var releaseId = release.ReleaseId;
            var info = infosByReleaseId.GetValueOrDefault(releaseId);
            var metadata = metadataByReleaseId.GetValueOrDefault(releaseId);
            var classification = classificationsByReleaseId.GetValueOrDefault(releaseId);

            var renderModel = new ForumPostTemplateRenderModel
            {
                Release = ToReleaseModel(
                    release: release,
                    nfo: nfosByReleaseId.GetValueOrDefault(releaseId)?.Content,
                    mediaFiles: mediaFilesByReleaseId.GetValueOrDefault(releaseId, []),
                    externalIdentifiers: externalIdentifiersByReleaseId.GetValueOrDefault(
                        releaseId,
                        []
                    )
                ),
                ReleaseInfo =
                    info is null && metadata is null
                        ? ForumPostTemplateReleaseInfoModel.Empty
                        : ToReleaseInfoModel(release.Name, info, metadata),
                Classification = classification is null
                    ? ForumPostTemplateClassificationModel.Empty
                    : ToClassificationModel(classification),
                Uploads = uploadsByReleaseId.GetValueOrDefault(releaseId, []),
                LinkCrypters = allHostersLinkCryptersByReleaseId.GetValueOrDefault(releaseId, []),
            };

            renderDataByReleaseId[releaseId] = new ReleaseForumPostRenderData(
                RenderModel: renderModel,
                ImageLinks: imageLinksByReleaseId[releaseId]
            );
        }

        return renderDataByReleaseId;
    }

    private static ForumPostTemplateReleaseModel ToReleaseModel(
        ReleaseReadModel release,
        string? nfo,
        IReadOnlyList<ReleaseMediaFileReadModel> mediaFiles,
        IReadOnlyList<ReleaseExternalIdentifierReadModel> externalIdentifiers
    )
    {
        var mediaFileModels = mediaFiles.Select(ToMediaFileModel).ToList();
        var mainVideo = ReleaseMediaFileSelector.SelectMainVideo(mediaFiles);

        var steamAppId = FindIdentifierValue(externalIdentifiers, ExternalIdentifierType.Steam);
        var imdbId = FindIdentifierValue(externalIdentifiers, ExternalIdentifierType.Imdb);

        return new ForumPostTemplateReleaseModel
        {
            Name = release.Name,
            PrimaryLanguage = GetLanguageName(release.PrimaryLanguageCode),
            Nfo = nfo ?? string.Empty,
            SteamAppId = steamAppId,
            SteamUrl =
                steamAppId.Length == 0
                    ? string.Empty
                    : $"https://store.steampowered.com/app/{steamAppId}",
            ImdbId = imdbId,
            ImdbUrl = imdbId.Length == 0 ? string.Empty : $"https://www.imdb.com/title/{imdbId}",
            MainVideo = mainVideo is null
                ? ForumPostTemplateMediaFileModel.Empty
                : ToMediaFileModel(mainVideo),
            MediaFiles = mediaFileModels,
        };
    }

    private static string FindIdentifierValue(
        IReadOnlyList<ReleaseExternalIdentifierReadModel> externalIdentifiers,
        ExternalIdentifierType type
    )
    {
        return externalIdentifiers.FirstOrDefault(identifier => identifier.Type == type)?.Value
            ?? string.Empty;
    }

    private static ForumPostTemplateClassificationModel ToClassificationModel(
        ReleaseClassificationReadModel classification
    )
    {
        return new ForumPostTemplateClassificationModel
        {
            Title = classification.Title,
            ContentType = classification.ContentType.ToString(),
            Platform = ToEnumName(classification.Platform, ReleasePlatform.Unknown),
            Resolution = ToResolutionName(classification.Resolution),
            Source = ToEnumName(classification.Source, ReleaseSource.Unknown),
            Year = classification.Year,
            Season = classification.Season,
            Episode = classification.Episode,
            ReleaseGroupToken = classification.ReleaseGroupToken ?? string.Empty,
            IsMultiLanguage = classification.IsMultiLanguage,
        };
    }

    private static string ToEnumName<TEnum>(TEnum value, TEnum unknown)
        where TEnum : struct, Enum
    {
        return value.Equals(unknown) ? string.Empty : value.ToString();
    }

    private static string ToResolutionName(ReleaseResolution resolution)
    {
        var name = ToEnumName(resolution, ReleaseResolution.Unknown);

        return name.Length > 1 && name[0] == 'R' && char.IsDigit(name[1]) ? name[1..] : name;
    }

    public static string GetLanguageName(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return string.Empty;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageCode).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return languageCode;
        }
    }

    private static ForumPostTemplateMediaFileModel ToMediaFileModel(ReleaseMediaFileReadModel file)
    {
        var defaultAudio =
            file.AudioStreams.FirstOrDefault(stream => stream.IsDefault)
            ?? file.AudioStreams.FirstOrDefault();

        return new ForumPostTemplateMediaFileModel
        {
            Path = file.RelativePath,
            Extension = GetExtension(file.RelativePath),
            SizeBytes = file.SizeBytes,
            MediaInfo = file.MediaInfoText,
            Duration = file.Duration is null
                ? string.Empty
                : file.Duration.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
            Container = file.ContainerFormat ?? string.Empty,
            Video = file.VideoStream is null
                ? ForumPostTemplateVideoStreamModel.Empty
                : ToVideoStreamModel(file.VideoStream),
            DefaultAudio = defaultAudio is null
                ? ForumPostTemplateAudioStreamModel.Empty
                : ToAudioStreamModel(defaultAudio),
            AudioStreams = file.AudioStreams.Select(ToAudioStreamModel).ToList(),
            SubtitleStreams = file.SubtitleStreams.Select(ToSubtitleStreamModel).ToList(),
        };
    }

    private static string GetExtension(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.IsNullOrEmpty(extension)
            ? string.Empty
            : extension.TrimStart('.').ToLowerInvariant();
    }

    private static ForumPostTemplateVideoStreamModel ToVideoStreamModel(
        ReleaseVideoStreamReadModel stream
    )
    {
        var resolution =
            stream.Width is null || stream.Height is null
                ? string.Empty
                : $"{stream.Width}x{stream.Height}";

        return new ForumPostTemplateVideoStreamModel
        {
            Codec = stream.Codec,
            Profile = stream.CodecProfile ?? string.Empty,
            Width = stream.Width,
            Height = stream.Height,
            Resolution = resolution,
            Fps = stream.Fps,
            PixelFormat = stream.PixelFormat ?? string.Empty,
            Language = stream.Language ?? string.Empty,
            Title = stream.Title ?? string.Empty,
            BitrateKbps = stream.BitrateKbps,
        };
    }

    private static ForumPostTemplateAudioStreamModel ToAudioStreamModel(
        ReleaseAudioStreamReadModel stream
    )
    {
        return new ForumPostTemplateAudioStreamModel
        {
            Codec = stream.Codec,
            Profile = stream.CodecProfile ?? string.Empty,
            Language = stream.Language ?? string.Empty,
            Title = stream.Title ?? string.Empty,
            ChannelLayout = stream.ChannelLayout ?? string.Empty,
            Channels = stream.Channels,
            SampleRate = stream.SampleRate,
            BitrateKbps = stream.BitrateKbps,
            IsDefault = stream.IsDefault,
        };
    }

    private static ForumPostTemplateSubtitleStreamModel ToSubtitleStreamModel(
        ReleaseSubtitleStreamReadModel stream
    )
    {
        return new ForumPostTemplateSubtitleStreamModel
        {
            Codec = stream.Codec,
            Language = stream.Language ?? string.Empty,
            Title = stream.Title ?? string.Empty,
            Forced = stream.Forced,
            IsDefault = stream.IsDefault,
        };
    }

    private static ForumPostTemplateReleaseInfoModel ToReleaseInfoModel(
        string releaseName,
        ReleaseInfoReadModel? info,
        ReleaseMetadataReadModel? metadata
    )
    {
        var size = info?.SizeNumber is null
            ? string.Empty
            : $"{info.SizeNumber} {info.SizeUnit}".Trim();

        var externalInfos =
            info?.ExternalInfos.Select(externalInfo => new ForumPostTemplateExternalInfoModel
                {
                    Type = externalInfo.Type.ToString(),
                    Title = externalInfo.Title ?? string.Empty,
                    Urls = externalInfo.Urls.Select(url => url.Url).ToList(),
                })
                .ToList()
            ?? [];

        return new ForumPostTemplateReleaseInfoModel
        {
            ReleaseName = info?.ReleaseName ?? metadata?.Title ?? releaseName,
            DatabaseUrl = info?.ReleaseDatabaseUrl ?? metadata?.MetadataDatabaseUrl ?? string.Empty,
            Size = size,
            SizeNumber = info?.SizeNumber,
            SizeUnit = info?.SizeUnit ?? string.Empty,
            VideoType = info?.VideoType ?? string.Empty,
            AudioType = info?.AudioType ?? string.Empty,
            Genre = metadata?.Genre ?? string.Empty,
            Description = metadata?.Description ?? string.Empty,
            Video = new ForumPostTemplateMediaInfoModel
            {
                Type = info?.VideoType ?? string.Empty,
                Format = info?.VideoType ?? string.Empty,
            },
            Audio = new ForumPostTemplateMediaInfoModel
            {
                Type = info?.AudioType ?? string.Empty,
                Format = info?.AudioType ?? string.Empty,
            },
            ExternalInfos = externalInfos,
        };
    }
}
