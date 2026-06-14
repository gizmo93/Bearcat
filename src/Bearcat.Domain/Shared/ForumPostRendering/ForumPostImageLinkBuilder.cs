using System.Text;
using Scriban.Runtime;

namespace Bearcat.Domain.Shared.ForumPostRendering;

public class ForumPostImageLinkBuilder(IForumPostImageLinkRepository repository)
{
    public static IReadOnlyList<ForumPostTemplateVariableReadModel> Variables { get; } =
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

    public async Task<ScriptObject> BuildForReleaseAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var imageLinks = await repository.GetReleaseImageLinksAsync(releaseId, cancellationToken);
        return ToScriptObject(imageLinks);
    }

    public async Task<ScriptObject> BuildForCollectionAsync(
        int releaseCollectionId,
        CancellationToken cancellationToken = default
    )
    {
        var imageLinks = await repository.GetCollectionImageLinksAsync(
            releaseCollectionId,
            cancellationToken
        );
        return ToScriptObject(imageLinks);
    }

    private static ScriptObject ToScriptObject(
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks
    )
    {
        var result = new ScriptObject();

        foreach (var imageLink in imageLinks)
        {
            var configLinks = new ScriptObject();

            foreach (var url in imageLink.Urls)
            {
                configLinks[NormalizeScriptKey(url.Size.ToString())] = url.Url;
            }

            var normalizedConfigName = NormalizeScriptKey(imageLink.ImageUploadConfigName);

            result.TryAdd(normalizedConfigName, configLinks);

            if (
                !string.Equals(
                    normalizedConfigName,
                    imageLink.ImageUploadConfigName,
                    StringComparison.Ordinal
                )
            )
            {
                result.TryAdd(imageLink.ImageUploadConfigName, configLinks);
            }
        }

        return result;
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
