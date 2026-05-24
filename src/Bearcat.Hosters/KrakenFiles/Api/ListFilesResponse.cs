namespace Bearcat.Hosters.KrakenFiles.Api;

public record ListFilesResponse
{
    public int Status { get; init; }

    public int? PerPage { get; init; }

    public int? Page { get; init; }

    public int? TotalItems { get; init; }

    public int? TotalPages { get; init; }

    public IReadOnlyList<FileData>? Data { get; init; }

    public string? Message { get; init; }
}
