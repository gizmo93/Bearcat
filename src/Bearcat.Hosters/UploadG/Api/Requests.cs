using System.Text.Json.Serialization;

namespace Bearcat.Hosters.UploadG.Api;

public record MultipartCreateRequest(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("mime")] string Mime,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("extension")] string Extension
);

public record BatchSignPartUrlsRequest(
    [property: JsonPropertyName("partNumbers")] IReadOnlyList<int> PartNumbers,
    [property: JsonPropertyName("uploadId")] string UploadId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("storageBucket")] string StorageBucket
);

public record MultipartCompleteRequest(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("uploadId")] string UploadId,
    [property: JsonPropertyName("storageBucket")] string StorageBucket,
    [property: JsonPropertyName("parts")] IReadOnlyList<UploadedPart> Parts
);

public record CreateS3EntryRequest(
    [property: JsonPropertyName("clientName")] string ClientName,
    [property: JsonPropertyName("clientExtension")] string ClientExtension,
    [property: JsonPropertyName("clientMime")] string ClientMime,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("parentId")] long? ParentId,
    [property: JsonPropertyName("storageBucket")] string StorageBucket
);

public record CreateFolderRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("parentId")] long? ParentId
);

public record CreateShareableLinkRequest(
    [property: JsonPropertyName("allow_download")] bool AllowDownload,
    [property: JsonPropertyName("allow_edit")] bool AllowEdit
);
