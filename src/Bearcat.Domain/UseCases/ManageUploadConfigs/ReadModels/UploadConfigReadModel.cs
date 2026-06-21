namespace Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;

public record UploadConfigReadModel(
    int UploadConfigId,
    string Name,
    string HosterRegistrationName,
    int HosterRegistrationId,
    int ArchiveConfigId,
    string ArchiveConfigName,
    string ReleaseName,
    bool PremiumOnlyDownload
);
