using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseListDto(
    int ReleaseId,
    string Name,
    ReleaseType ReleaseType,
    int ArchiveConfigCount,
    int UploadConfigCount,
    string ReleaseFolderPath);
