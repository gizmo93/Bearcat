using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseOverviewImageUploadUrlReadModel(ImageSize ImageSize, string Url);
