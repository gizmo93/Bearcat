using System.Text.Json.Serialization;

namespace Bearcat.Hosters.CloudFam.Api;

public record DeleteFolderRequest(
    [property: JsonPropertyName("folder_id")] long FolderId,
    [property: JsonPropertyName("action_on_files")] string ActionOnFiles
);
