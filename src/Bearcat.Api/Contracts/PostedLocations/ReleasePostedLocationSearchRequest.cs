namespace Bearcat.Api.Contracts.PostedLocations;

public record ReleasePostedLocationSearchRequest
{
    public int PageIndex { get; init; }

    public int PageSize { get; init; } = 10;
}
