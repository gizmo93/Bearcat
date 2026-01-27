namespace Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;

public record UploadConfigDto(
    int UploadConfigId,
    string Name,
    string HosterRegistrationName,
    int HosterRegistrationId,
    int ArchiveConfigId,
    string ArchiveConfigName,
    string ReleaseName,
    IReadOnlyList<string> LinksDistributedTo);
