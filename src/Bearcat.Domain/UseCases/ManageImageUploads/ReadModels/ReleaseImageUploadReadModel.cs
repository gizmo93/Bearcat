using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;

public record ReleaseImageUploadReadModel(
    int ImageUploadId,
    string ImageUploadConfigName,
    string ImageHosterRegistrationName,
    DateTime CreatedAt,
    DateTime? UploadedAt,
    UploadState UploadState,
    int UrlCount,
    IReadOnlyList<string> ErrorMessages
);
