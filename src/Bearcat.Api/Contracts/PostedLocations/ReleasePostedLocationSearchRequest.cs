namespace Bearcat.Api.Contracts.PostedLocations;

public record ReleasePostedLocationSearchRequest
{
    public int PageIndex { get; init; } = 0;

    public int PageSize { get; init; } = 10;
}
