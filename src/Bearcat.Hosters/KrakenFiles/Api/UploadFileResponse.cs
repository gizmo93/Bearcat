namespace Bearcat.Hosters.KrakenFiles.Api;

public record UploadFileResponse
{
    public int Status { get; init; }

    public UploadFileData? Data { get; init; }
}

public record UploadFileData
{
    public string? Url { get; init; }

    public string? Hash { get; init; }

    public string? Title { get; init; }

    public long? Size { get; init; }

    public string? FolderId { get; init; }

    public string? EmbedUrl { get; init; }

    public string? Message { get; init; }
}
