using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

public record UploadSearchRequest
{
    /// <summary>
    /// Only uploads completed after this timestamp are returned,
    /// sorted ascending by uploadedAt.
    /// </summary>
    public DateTime? UploadedAfter { get; init; }

    public UploadState? UploadState { get; init; }

    public OnlineState? OnlineState { get; init; }

    public int? HosterRegistrationId { get; init; }

    public int? ReleaseId { get; init; }

    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}
