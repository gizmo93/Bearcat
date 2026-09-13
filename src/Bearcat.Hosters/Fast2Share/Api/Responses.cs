using System.Text.Json.Serialization;

namespace Bearcat.Hosters.Fast2Share.Api;

public record CreateUploadResponse(
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("deduped")] bool? Deduped,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("share_url")] string? ShareUrl,
    [property: JsonPropertyName("upload_url")] string? UploadUrl,
    [property: JsonPropertyName("upload_token")] string? UploadToken,
    [property: JsonPropertyName("protocol")] string? Protocol,
    [property: JsonPropertyName("metadata_key")] string? MetadataKey,
    [property: JsonPropertyName("error")] string? Error
);

public record FileStatusResponse(
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("share_url")] string? ShareUrl
);

public record FileResponse(
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("downloads")] int? Downloads,
    [property: JsonPropertyName("share_url")] string? ShareUrl
);

public record FolderResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parent_id")] long? ParentId
);

public record FolderListResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<FolderResponse>? Data
);

public record MoveFileResponse(
    [property: JsonPropertyName("uuid")] string? Uuid,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("share_url")] string? ShareUrl,
    [property: JsonPropertyName("folder_id")] long? FolderId
);

public record UserResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("plan")] UserPlanResponse? Plan
);

public record UserPlanResponse(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("max_file_size")] long? MaxFileSize,
    [property: JsonPropertyName("storage_quota")] long? StorageQuota
);

public record ErrorResponse([property: JsonPropertyName("error")] string? Error);
