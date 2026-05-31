using System.Text.Json.Serialization;

namespace Bearcat.Hosters.UploadG.Api;

public record SpaceUsageResponse(
    [property: JsonPropertyName("usedSpace")] long UsedSpace,
    [property: JsonPropertyName("availableSpace")] long AvailableSpace,
    [property: JsonPropertyName("percentUsed")] decimal? PercentUsed
);

public record MultipartCreateResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("uploadId")] string? UploadId,
    [property: JsonPropertyName("storageBucket")] string? StorageBucket,
    [property: JsonPropertyName("acl")] string? Acl
);

public record BatchSignPartUrlsResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("urls")] IReadOnlyList<SignedPartUrl>? Urls
);

public record SignedPartUrl(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("partNumber")] int PartNumber
);

public record UploadedPart(
    [property: JsonPropertyName("ETag")] string ETag,
    [property: JsonPropertyName("PartNumber")] int PartNumber
);

public record StatusResponse([property: JsonPropertyName("status")] string? Status);

public record UploadFileResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("fileEntry")] FileEntry? FileEntry
);

public record CreateFolderResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("folder")] FileEntry? Folder
);

public record FileEntry(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("parent_id")] long? ParentId,
    [property: JsonPropertyName("type")] string? Type
);

public record FileEntryListResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<FileEntry> Data
);

public record ShareableLinkResponse(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("link")] ShareableLink? Link
);

public record ShareableLink(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("hash")] string? Hash,
    [property: JsonPropertyName("entry_id")] long EntryId
);
