namespace Bearcat.Api.Contracts.Uploads;

public record ReleaseUploadSearchRequest
{
    public int? UploadConfigId { get; init; }

    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}
