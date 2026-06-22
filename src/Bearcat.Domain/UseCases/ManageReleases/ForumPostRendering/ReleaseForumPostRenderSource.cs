using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public class ReleaseForumPostRenderSource(
    IReleaseReadRepository releaseReadRepository,
    ReleaseForumPostUploadBuilder uploadBuilder,
    ForumPostImageLinkBuilder imageLinkBuilder
) : IForumPostRenderSource
{
    public ForumPostTemplateType Type => ForumPostTemplateType.Release;

    public IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables()
    {
        var variables = ForumPostTemplateVariableCatalog
            .GetVariables(typeof(ForumPostTemplateRenderModel))
            .ToList();
        variables.AddRange(ForumPostImageLinkBuilder.Variables);
        return variables;
    }

    public async Task<ScriptObject?> BuildGlobalsAsync(
        int entityId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await releaseReadRepository.GetReleaseAsync(entityId, cancellationToken);

        if (release is null)
        {
            return null;
        }

        var info = await releaseReadRepository.GetReleaseInfoAsync(entityId, cancellationToken);

        var nfo = (
            await releaseReadRepository.GetReleaseNfoAsync(entityId, cancellationToken)
        )?.Content;

        var uploads = await uploadBuilder.BuildAsync(entityId, cancellationToken);

        var mediaFiles = await releaseReadRepository.GetMediaFilesAsync(
            entityId,
            cancellationToken
        );

        var renderModel = new ForumPostTemplateRenderModel
        {
            Release = ToReleaseModel(release, nfo, mediaFiles),
            ReleaseInfo = info is null
                ? ForumPostTemplateReleaseInfoModel.Empty
                : ToReleaseInfoModel(info),
            Uploads = uploads,
        };

        var scriptObject = new ScriptObject();
        scriptObject.Import(renderModel, ForumPostTemplateVariableCatalog.ShouldExposeMember);
        scriptObject["imagelinks"] = await imageLinkBuilder.BuildForReleaseAsync(
            entityId,
            cancellationToken
        );

        return scriptObject;
    }

    private static ForumPostTemplateReleaseModel ToReleaseModel(
        ReleaseReadModel release,
        string? nfo,
        IReadOnlyList<ReleaseMediaFileReadModel> mediaFiles
    )
    {
        var mediaFileModels = mediaFiles.Select(ToMediaFileModel).ToList();
        var mainVideo = ReleaseMediaFileSelector.SelectMainVideo(mediaFiles);

        return new ForumPostTemplateReleaseModel
        {
            Name = release.Name,
            Nfo = nfo ?? string.Empty,
            MainVideo = mainVideo is null
                ? ForumPostTemplateMediaFileModel.Empty
                : ToMediaFileModel(mainVideo),
            MediaFiles = mediaFileModels,
        };
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
                : file.Duration.Value.ToString(@"hh\:mm\:ss"),
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

    private static ForumPostTemplateReleaseInfoModel ToReleaseInfoModel(ReleaseInfoReadModel info)
    {
        var size = info.SizeNumber is null
            ? string.Empty
            : $"{info.SizeNumber} {info.SizeUnit}".Trim();

        var externalInfos = info
            .ExternalInfos.Select(externalInfo => new ForumPostTemplateExternalInfoModel
            {
                Type = externalInfo.Type.ToString(),
                Title = externalInfo.Title ?? string.Empty,
                Urls = externalInfo.Urls.Select(url => url.Url).ToList(),
            })
            .ToList();

        return new ForumPostTemplateReleaseInfoModel
        {
            ReleaseName = info.ReleaseName,
            DatabaseUrl = info.ReleaseDatabaseUrl ?? string.Empty,
            Size = size,
            SizeNumber = info.SizeNumber,
            SizeUnit = info.SizeUnit ?? string.Empty,
            VideoType = info.VideoType ?? string.Empty,
            AudioType = info.AudioType ?? string.Empty,
            Genre = info.Genre ?? string.Empty,
            Description = info.Description ?? string.Empty,
            Video = new ForumPostTemplateMediaInfoModel
            {
                Type = info.VideoType ?? string.Empty,
                Format = info.VideoType ?? string.Empty,
            },
            Audio = new ForumPostTemplateMediaInfoModel
            {
                Type = info.AudioType ?? string.Empty,
                Format = info.AudioType ?? string.Empty,
            },
            ExternalInfos = externalInfos,
        };
    }
}
