using System.Text.Json.Serialization;

namespace Bearcat.Hosters.CloudFam.Api;

public record CloudFamResponse<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("data")] T? Data
);

public record UserProfileResponse(
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("account_status")] string? AccountStatus,
    [property: JsonPropertyName("storage_used_bytes")] long? StorageUsedBytes,
    [property: JsonPropertyName("storage_limit_gb")] long? StorageLimitGb
);

public record UploadSessionResponse(
    [property: JsonPropertyName("upload_url")] string? UploadUrl,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn
);

public record FinalizeUploadResponse(
    [property: JsonPropertyName("file_id")] long FileId,
    [property: JsonPropertyName("filename")] string? Filename,
    [property: JsonPropertyName("file_size")] long? FileSize,
    [property: JsonPropertyName("download_link")] string? DownloadLink
);

public record FolderResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parent_id")] long? ParentId,
    [property: JsonPropertyName("total_files")] int? TotalFiles
);

public record FolderListResponse(
    [property: JsonPropertyName("folders")] IReadOnlyList<FolderResponse>? Folders,
    [property: JsonPropertyName("root_files")] int? RootFiles
);

public record CreateFolderResponse(
    [property: JsonPropertyName("folder_id")] long FolderId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parent_id")] long? ParentId
);

public record MoveFilesResponse(
    [property: JsonPropertyName("folder_id")] long? FolderId,
    [property: JsonPropertyName("moved_count")] int MovedCount
);

public record FileListItemResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("original_filename")] string? OriginalFilename,
    [property: JsonPropertyName("file_size_bytes")] long? FileSizeBytes,
    [property: JsonPropertyName("download_count")] int? DownloadCount,
    [property: JsonPropertyName("short_url_id")] string? ShortUrlId,
    [property: JsonPropertyName("download_url")] string? DownloadUrl,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("uploaded_at")] string? UploadedAt
);

public record PaginationResponse(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("total_pages")] int TotalPages
);

public record FileListResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] IReadOnlyList<FileListItemResponse>? Data,
    [property: JsonPropertyName("pagination")] PaginationResponse? Pagination
);

public record FileInfoItemResponse(
    [property: JsonPropertyName("filecode")] string? FileCode,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("downloads")] int? Downloads,
    [property: JsonPropertyName("is_alive")] bool IsAlive,
    [property: JsonPropertyName("status_text")] string? StatusText
);

public record FileInfoResponse(
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("result")] IReadOnlyList<FileInfoItemResponse>? Result,
    [property: JsonPropertyName("msg")] string? Message
);

public record ErrorResponse(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("error")] ErrorDetailResponse? Error
);

public record ErrorDetailResponse(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("message")] string? Message
);
