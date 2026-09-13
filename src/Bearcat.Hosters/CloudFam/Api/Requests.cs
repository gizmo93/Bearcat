using System.Text.Json.Serialization;

namespace Bearcat.Hosters.CloudFam.Api;

public record FinalizeUploadRequest(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("original_filename")] string OriginalFilename,
    [property: JsonPropertyName("file_size")] long FileSize
);

public record CreateFolderRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent_id")] long? ParentId
);

public record MoveFilesRequest(
    [property: JsonPropertyName("file_ids")] IReadOnlyList<long> FileIds,
    [property: JsonPropertyName("folder_id")] long? FolderId
);
