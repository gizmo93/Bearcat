using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ReleaseDto(int ReleaseId, string Name, ReleaseType ReleaseType, string ReleaseFolderPath);
