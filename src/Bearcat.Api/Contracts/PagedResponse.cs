namespace Bearcat.Api.Contracts;

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageIndex,
    int PageSize,
    int TotalPages
);
