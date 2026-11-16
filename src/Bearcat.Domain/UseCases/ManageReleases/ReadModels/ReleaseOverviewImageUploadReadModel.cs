using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOverviewImageUploadReadModel(
    int ImageUploadConfigId,
    string ImageUploadConfigName,
    string ImageHosterRegistrationName,
    bool UploadCover,
    int? ImageUploadId,
    DateTime? CreatedAt,
    DateTime? UploadedAt,
    UploadState? UploadState,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyList<ReleaseOverviewImageUploadUrlReadModel> ImageUrls,
    IReadOnlyList<ReleaseOverviewScreenshotImageUploadReadModel> ScreenshotImageUploads
);
