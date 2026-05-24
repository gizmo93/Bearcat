namespace Bearcat.Hosters.KrakenFiles.Api;

public record AvailableServerResponse
{
    public int Status { get; init; }

    public AvailableServerData? Data { get; init; }
}

public record AvailableServerData
{
    public string? Url { get; init; }

    public string? ServerAccessToken { get; init; }

    public string? Message { get; init; }
}
