using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fast2Share.Api;

public record CreateUploadRequest(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("fingerprint")] string? Fingerprint,
    [property: JsonPropertyName("sha256")] string? Sha256
);

public record ConfirmUploadRequest([property: JsonPropertyName("sha256")] string Sha256);

public record CreateFolderRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parent_id")] long? ParentId
);

public record MoveFileRequest([property: JsonPropertyName("folder_id")] long FolderId);
