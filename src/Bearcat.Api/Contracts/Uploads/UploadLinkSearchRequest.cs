using Bearcat.Domain.ValueObjects;

namespace Bearcat.Api.Contracts.Uploads;

public record UploadLinkSearchRequest
{
    /// <summary>
    /// Only return links with this online state.
    /// </summary>
    public OnlineState? OnlineState { get; init; }

    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}
