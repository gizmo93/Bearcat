namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOnlineStateCounts(
    int TotalCount,
    int OnlineCount,
    int PartiallyOnlineCount,
    int OfflineCount,
    int WithoutUploadConfigsCount
);
