using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases;

public record ReleaseUploadConfigMatch(
    UploadConfigTemplate UploadConfigTemplate,
    UploadConfig UploadConfig
);
