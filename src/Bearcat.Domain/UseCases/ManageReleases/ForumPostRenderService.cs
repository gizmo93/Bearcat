using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class ForumPostRenderService(
    IForumPostTemplateReadRepository templateReadRepository,
    IReleaseReadRepository releaseReadRepository
)
{
    public static IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables()
    {
        return ForumPostTemplateVariableCatalog.GetVariables();
    }

    public async Task<ForumPostTemplateRenderResult> RenderAsync(
        int releaseId,
        int forumPostTemplateId,
        CancellationToken cancellationToken = default
    )
    {
        var template = await templateReadRepository.GetDetailAsync(
            forumPostTemplateId: forumPostTemplateId,
            cancellationToken: cancellationToken
        );

        if (template is null)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: ["Forum post template not found."]
            );
        }

        var renderContext = await BuildRenderContextAsync(releaseId, cancellationToken);
        return await RenderBodyAsync(template.TemplateBody, renderContext);
    }

    private async Task<ForumPostTemplateRenderResult> RenderBodyAsync(
        string templateBody,
        ForumPostTemplateRenderContext renderContext
    )
    {
        var template = Template.Parse(templateBody);

        if (template.HasErrors)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: template.Messages.Select(message => message.ToString()).ToList()
            );
        }

        var scriptObject = new ScriptObject();

        scriptObject.Import(
            renderContext.RenderModel,
            ForumPostTemplateVariableCatalog.ShouldExposeMember
        );

        scriptObject["imagelinks"] = renderContext.ImageLinks;

        var context = new TemplateContext
        {
            StrictVariables = false,
            EnableRelaxedTargetAccess = true,
            EnableRelaxedMemberAccess = true,
            EnableRelaxedIndexerAccess = true,
            MemberFilter = ForumPostTemplateVariableCatalog.ShouldExposeMember,
        };

        context.PushGlobal(scriptObject);

        try
        {
            var result = await template.RenderAsync(context);
            return new ForumPostTemplateRenderResult(Content: result, Errors: []);
        }
        catch (Exception exception)
            when (exception is ScriptRuntimeException or InvalidOperationException)
        {
            return new ForumPostTemplateRenderResult(
                Content: string.Empty,
                Errors: [exception.Message]
            );
        }
    }

    private async Task<ForumPostTemplateRenderContext> BuildRenderContextAsync(
        int releaseId,
        CancellationToken cancellationToken
    )
    {
        var release = await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken);

        if (release is null)
        {
            return ForumPostTemplateRenderContext.Empty;
        }

        var overview = await releaseReadRepository.GetReleaseOverviewAsync(
            releaseId: releaseId,
            cancellationToken: cancellationToken
        );

        var imageUploads = await releaseReadRepository.GetReleaseOverviewImageUploadsAsync(
            releaseId: releaseId,
            cancellationToken: cancellationToken
        );

        var info = await releaseReadRepository.GetReleaseInfoAsync(releaseId, cancellationToken);

        var nfo = (
            await releaseReadRepository.GetReleaseNfoAsync(releaseId, cancellationToken)
        )?.Content;

        var uploadModels = new List<ForumPostTemplateUploadModel>();

        foreach (var upload in overview)
        {
            uploadModels.Add(await ToUploadModelAsync(releaseId, upload, cancellationToken));
        }

        return new ForumPostTemplateRenderContext(
            RenderModel: new ForumPostTemplateRenderModel
            {
                Release = ToReleaseModel(release, nfo),
                ReleaseInfo = info is null
                    ? ForumPostTemplateReleaseInfoModel.Empty
                    : ToReleaseInfoModel(info),
                Uploads = uploadModels,
            },
            ImageLinks: ToImageLinksScriptObject(imageUploads)
        );
    }

    private async Task<ForumPostTemplateUploadModel> ToUploadModelAsync(
        int releaseId,
        ReleaseOverviewUploadReadModel upload,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> directLinks = upload.UploadId is null
            ? []
            : await releaseReadRepository.GetUploadLinksAsync(
                releaseId: releaseId,
                uploadId: upload.UploadId.Value,
                cancellationToken: cancellationToken
            );

        var linkCrypters = upload
            .LinkCrypterLinks.Select(link => new ForumPostTemplateLinkCrypterModel
            {
                Name = link.LinkCrypterRegistrationName,
                ContainerLink = link.ContainerUrl,
                CreatedAt = link.CreatedAt,
            })
            .ToList();

        return new ForumPostTemplateUploadModel
        {
            Name = upload.UploadConfigName,
            HosterName = upload.HosterRegistrationName,
            UploadedAt = upload.UploadedAt,
            ArchivePassword = upload.ArchivePassword ?? string.Empty,
            Links = directLinks,
            LinkCrypters = linkCrypters,
        };
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

    private static ScriptObject ToImageLinksScriptObject(
        IReadOnlyList<ReleaseOverviewImageUploadReadModel> imageUploads
    )
    {
        var imageLinks = new ScriptObject();

        foreach (var imageUpload in imageUploads)
        {
            var configLinks = new ScriptObject();

            foreach (var imageUrl in imageUpload.ImageUrls)
            {
                configLinks[NormalizeScriptKey(imageUrl.ImageSize.ToString())] = imageUrl.Url;
            }

            var normalizedConfigName = NormalizeScriptKey(imageUpload.ImageUploadConfigName);

            AddImageUploadConfigLinks(
                imageLinks: imageLinks,
                key: normalizedConfigName,
                configLinks: configLinks
            );

            if (
                !string.Equals(
                    normalizedConfigName,
                    imageUpload.ImageUploadConfigName,
                    StringComparison.Ordinal
                )
            )
            {
                AddImageUploadConfigLinks(
                    imageLinks: imageLinks,
                    key: imageUpload.ImageUploadConfigName,
                    configLinks: configLinks
                );
            }
        }

        return imageLinks;
    }

    private static void AddImageUploadConfigLinks(
        ScriptObject imageLinks,
        string key,
        ScriptObject configLinks
    )
    {
        imageLinks.TryAdd(key, configLinks);
    }

    private static string NormalizeScriptKey(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        var normalized = builder.ToString().Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "_";
        }

        return char.IsDigit(normalized[0]) ? $"_{normalized}" : normalized;
    }
}
