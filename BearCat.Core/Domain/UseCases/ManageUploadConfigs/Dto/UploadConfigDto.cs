namespace BearCat.Core.Domain.UseCases.ManageUploadConfigs.Dto;

public record UploadConfigDto(
    int UploadConfigId,
    string Name,
    string HosterRegistrationName,
    int HosterRegistrationId,
    int ArchiveConfigId,
    string ArchiveConfigName,
    IReadOnlyList<string> LinksDistributedTo);
