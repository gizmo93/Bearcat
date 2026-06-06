using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOverviewImageUploadReadModel(
    int ImageUploadConfigId,
    string ImageUploadConfigName,
    string ImageHosterRegistrationName,
    int? ImageUploadId,
    DateTime? CreatedAt,
    DateTime? UploadedAt,
    UploadState? UploadState,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyList<ReleaseOverviewImageUploadUrlReadModel> ImageUrls
);
