namespace Bearcat.Website.Shared;

public record SearchUrlState<TQuery>(TQuery Query, int PageIndex, int PageSize);
