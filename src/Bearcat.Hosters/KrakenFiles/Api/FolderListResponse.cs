namespace Bearcat.Hosters.KrakenFiles.Api;

public record FolderListResponse
{
    public int Status { get; init; }

    public IReadOnlyList<FolderData>? Data { get; init; }

    public string? Message { get; init; }
}

public record FolderData
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public string? ParentId { get; init; }
}
