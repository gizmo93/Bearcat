using System.Text;
using Bearcat.Domain.Shared.ForumPostRendering;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Scriban.Runtime;

namespace Bearcat.Domain.UseCases.ManageReleases.ForumPostRendering;

public class ReleaseForumPostRenderSource(
    IReleaseReadRepository releaseReadRepository,
    ReleaseForumPostUploadBuilder uploadBuilder
) : IForumPostRenderSource
{
    private static readonly IReadOnlyList<ForumPostTemplateVariableReadModel> ImageLinkVariables =
    [
        new(
            "{{ imagelinks.<image_upload_config_name>.full }}",
            "Full image URL by image upload configuration name. The configuration name is normalized to lower snake case."
        ),
        new(
            "{{ imagelinks.<image_upload_config_name>.medium }}",
            "Medium image URL by image upload configuration name."
        ),
        new(
            "{{ imagelinks.<image_upload_config_name>.thumbnail }}",
            "Thumbnail image URL by image upload configuration name."
        ),
        new(
            "{{ imagelinks[\"Image Upload Config Name\"].full }}",
            "Full image URL using the original image upload configuration name."
        ),
    ];

    public ForumPostTemplateType Type => ForumPostTemplateType.Release;

    public IReadOnlyList<ForumPostTemplateVariableReadModel> GetVariables()
    {
        var variables = ForumPostTemplateVariableCatalog
            .GetVariables(typeof(ForumPostTemplateRenderModel))
            .ToList();
        variables.AddRange(ImageLinkVariables);
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

        var imageUploads = await releaseReadRepository.GetReleaseOverviewImageUploadsAsync(
            releaseId: entityId,
            cancellationToken: cancellationToken
        );

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
        scriptObject["imagelinks"] = ToImageLinksScriptObject(imageUploads);

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

            imageLinks.TryAdd(normalizedConfigName, configLinks);

            if (
                !string.Equals(
                    normalizedConfigName,
                    imageUpload.ImageUploadConfigName,
                    StringComparison.Ordinal
                )
            )
            {
                imageLinks.TryAdd(imageUpload.ImageUploadConfigName, configLinks);
            }
        }

        return imageLinks;
    }

    private static string NormalizeScriptKey(string value)
    {
        var builder = new StringBuilder(value.Length);

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
