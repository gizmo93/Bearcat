namespace Bearcat.Hosters.KrakenFiles.Api;

public record FileResponse
{
    public int Status { get; init; }

    public FileData? Data { get; init; }

    public string? Message { get; init; }
}

public record FileData
{
    public string? Url { get; init; }

    public string? Name { get; init; }

    public string? Hash { get; init; }

    public int? Downloads { get; init; }

    public string? Message { get; init; }
}
