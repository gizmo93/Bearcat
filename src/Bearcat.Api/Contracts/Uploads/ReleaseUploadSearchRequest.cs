namespace Bearcat.Api.Contracts.Uploads;

public record ReleaseUploadSearchRequest
{
    /// <summary>
    /// Only return uploads of this upload configuration.
    /// </summary>
    public int? UploadConfigId { get; init; }

    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}
