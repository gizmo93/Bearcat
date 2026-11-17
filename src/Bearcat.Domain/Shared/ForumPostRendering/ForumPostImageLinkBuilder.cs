using System.Globalization;
using System.Text;
using Scriban.Runtime;

namespace Bearcat.Domain.Shared.ForumPostRendering;

public static class ForumPostImageLinkBuilder
{
    public static ForumPostTemplateVariableNode CreateReleaseVariableNode(string path)
    {
        var variableNode = CreateVariableNode(path);

        return variableNode with
        {
            Children =
            [
                .. variableNode.Children,
                ForumPostTemplateVariableNode.CreateLoopNode(
                    name: "<image_upload_config_name>.screenshots",
                    path: $"{path}.<image_upload_config_name>.screenshots",
                    description: "Uploaded screenshots of this image upload configuration, ordered by number.",
                    loopVariable: "screenshot",
                    children:
                    [
                        ForumPostTemplateVariableNode.CreateValueNode(
                            name: "full",
                            path: "screenshot.full",
                            description: "Full screenshot image URL."
                        ),
                        ForumPostTemplateVariableNode.CreateValueNode(
                            name: "medium",
                            path: "screenshot.medium",
                            description: "Medium screenshot image URL."
                        ),
                        ForumPostTemplateVariableNode.CreateValueNode(
                            name: "thumbnail",
                            path: "screenshot.thumbnail",
                            description: "Thumbnail screenshot image URL."
                        ),
                        ForumPostTemplateVariableNode.CreateValueNode(
                            name: "index",
                            path: "screenshot.index",
                            description: "Screenshot number, starting at 1."
                        ),
                        ForumPostTemplateVariableNode.CreateValueNode(
                            name: "timestamp",
                            path: "screenshot.timestamp",
                            description: "Video position of the screenshot (hh:mm:ss)."
                        ),
                    ]
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "<image_upload_config_name>.screenshotgrid.full",
                    path: $"{path}.<image_upload_config_name>.screenshotgrid.full",
                    description: "Full screenshot grid image URL. Empty when the screenshot grid is not uploaded."
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "<image_upload_config_name>.screenshotgrid.medium",
                    path: $"{path}.<image_upload_config_name>.screenshotgrid.medium",
                    description: "Medium screenshot grid image URL."
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "<image_upload_config_name>.screenshotgrid.thumbnail",
                    path: $"{path}.<image_upload_config_name>.screenshotgrid.thumbnail",
                    description: "Thumbnail screenshot grid image URL."
                ),
            ],
        };
    }

    public static ForumPostTemplateVariableNode CreateVariableNode(string path)
    {
        return ForumPostTemplateVariableNode.CreateObjectNode(
            name: "imagelinks",
            path: path,
            description: "Image URLs by image upload configuration name.",
            children:
            [
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "<image_upload_config_name>.full",
                    path: $"{path}.<image_upload_config_name>.full",
                    description: "Full image URL by image upload configuration name. The configuration name is normalized to lower snake case."
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "<image_upload_config_name>.medium",
                    path: $"{path}.<image_upload_config_name>.medium",
                    description: "Medium image URL by image upload configuration name."
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "<image_upload_config_name>.thumbnail",
                    path: $"{path}.<image_upload_config_name>.thumbnail",
                    description: "Thumbnail image URL by image upload configuration name."
                ),
                ForumPostTemplateVariableNode.CreateValueNode(
                    name: "[\"Image Upload Config Name\"].full",
                    path: $"{path}[\"Image Upload Config Name\"].full",
                    description: "Full image URL using the original image upload configuration name."
                ),
            ]
        );
    }

    public static ScriptObject ToReleaseScriptObject(
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks
    )
    {
        return ToScriptObject(imageLinks: imageLinks, createConfigLinks: CreateReleaseConfigLinks);
    }

    public static ScriptObject ToCollectionScriptObject(
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks
    )
    {
        return ToScriptObject(
            imageLinks: imageLinks,
            createConfigLinks: imageLink => CreateSizeLinks(imageLink.Urls)
        );
    }

    private static ScriptObject ToScriptObject(
        IReadOnlyList<ForumPostImageLinkReadModel> imageLinks,
        Func<ForumPostImageLinkReadModel, ScriptObject> createConfigLinks
    )
    {
        var result = new ScriptObject();

        foreach (var imageLink in imageLinks)
        {
            var configLinks = createConfigLinks(imageLink);
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

    private static ScriptObject CreateReleaseConfigLinks(ForumPostImageLinkReadModel imageLink)
    {
        var configLinks = CreateSizeLinks(imageLink.Urls);
        var screenshots = new ScriptArray();

        foreach (var screenshot in imageLink.Screenshots)
        {
            var screenshotLinks = CreateSizeLinks(screenshot.Urls);
            screenshotLinks["index"] = screenshot.Index;

            screenshotLinks["timestamp"] = screenshot.Timestamp.ToString(
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture
            );

            screenshots.Add(screenshotLinks);
        }

        configLinks["screenshots"] = screenshots;

        if (imageLink.ScreenshotGridUrls.Count > 0)
        {
            configLinks["screenshotgrid"] = CreateSizeLinks(imageLink.ScreenshotGridUrls);
        }

        return configLinks;
    }

    private static ScriptObject CreateSizeLinks(IReadOnlyList<ForumPostImageLinkUrlReadModel> urls)
    {
        var sizeLinks = new ScriptObject();

        foreach (var url in urls)
        {
            sizeLinks[NormalizeScriptKey(url.Size.ToString())] = url.Url;
        }

        return sizeLinks;
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
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
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
