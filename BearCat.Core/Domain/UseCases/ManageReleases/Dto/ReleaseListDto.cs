using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.UseCases.ManageReleases.Dto;

public record ReleaseListDto(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    int ArchiveConfigCount,
    int UploadConfigCount,
    string ReleaseFolderPath,
    IReadOnlyList<ReleaseListDto.UploadConfigDto> UploadConfigStates)
{
    public record UploadConfigDto(string Name, OnlineState OnlineState);
}
