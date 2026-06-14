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

        var renderModel = new ForumPostTemplateRenderModel
        {
            Release = ToReleaseModel(release, nfo),
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
        string? nfo
    )
    {
        return new ForumPostTemplateReleaseModel { Name = release.Name, Nfo = nfo ?? string.Empty };
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
