namespace Bearcat.Domain.Shared;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int PageIndex, int PageSize)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
