using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.UseCases.ManageReleases.Dto;

public record ReleaseDto(int ReleaseId, string Name, ReleaseType ReleaseType, string ReleaseFolderPath);
