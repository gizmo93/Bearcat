namespace Bearcat.Hosters.KrakenFiles.Api;

public record FolderCreateResponse
{
    public int Status { get; init; }

    public FolderCreateData? Data { get; init; }
}

public record FolderCreateData
{
    public string? Message { get; init; }
}
