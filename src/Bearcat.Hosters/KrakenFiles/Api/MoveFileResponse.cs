namespace Bearcat.Hosters.KrakenFiles.Api;

public record MoveFileResponse
{
    public int Status { get; init; }

    public MoveFileData? Data { get; init; }
}

public record MoveFileData
{
    public string? Message { get; init; }
}
