namespace Bearcat.Domain.UseCases.ManagePostedLocations.Dto;

public record ReleasePostedLocationSearchQuery(int ReleaseId, int PageIndex = 0, int PageSize = 10);
