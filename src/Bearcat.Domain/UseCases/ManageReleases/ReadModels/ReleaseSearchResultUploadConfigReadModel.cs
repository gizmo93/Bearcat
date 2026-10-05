namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseSearchResultUploadConfigReadModel(
    int UploadConfigId,
    string HosterRegistrationName,
    bool IsOnline
);
