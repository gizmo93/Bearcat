using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseSearchResultUploadConfigReadModel(
    int UploadConfigId,
    string UploadTargetName,
    OnlineState OnlineState
);
