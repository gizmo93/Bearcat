using Bearcat.Abstractions.ImageHoster.Results;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;

public record CollectionImageUploadReadModel(
    int ImageUploadConfigId,
    string Name,
    int ImageHosterRegistrationId,
    string ImageHosterRegistrationName,
    int? ImageUploadId,
    DateTime? CreatedAt,
    DateTime? UploadedAt,
    UploadState? UploadState,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyList<CollectionImageUploadUrlReadModel> ImageUrls
);

public record CollectionImageUploadUrlReadModel(ImageSize ImageSize, string Url);
