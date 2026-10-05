using Bearcat.Domain.UseCases.ManageReleases.ReadModels;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public sealed record ReleaseMirrorOnlineCount(int OnlineCount, int TotalCount)
{
    public bool HasUploads => TotalCount > 0;

    public bool AreAllOnline => OnlineCount == TotalCount;

    public string Text => $"{OnlineCount}/{TotalCount}";

    public static ReleaseMirrorOnlineCount From(
        IReadOnlyList<ReleaseSearchResultUploadConfigReadModel> uploadConfigs
    ) => new(uploadConfigs.Count(uploadConfig => uploadConfig.IsOnline), uploadConfigs.Count);
}
