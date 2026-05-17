namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseUploadSearchQuery(
    int ReleaseId,
    int? UploadConfigId = null,
    int PageIndex = 0,
    int PageSize = 10
);
