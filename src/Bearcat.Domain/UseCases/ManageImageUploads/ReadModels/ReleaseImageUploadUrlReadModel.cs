using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;

public record ReleaseImageUploadUrlReadModel(ImageSize ImageSize, string Url);
