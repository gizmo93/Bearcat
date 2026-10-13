namespace Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;

public record UploadConfigReadModel(
    int UploadConfigId,
    string Name,
    int? HosterRegistrationId,
    string? HosterRegistrationName,
    int? HosterAccountPoolId,
    string? HosterAccountPoolName,
    int ArchiveConfigId,
    string ArchiveConfigName,
    string ReleaseName,
    bool PremiumOnlyDownload,
    int? TotalIndividualDownloads,
    double? TotalCompleteDownloads
);
