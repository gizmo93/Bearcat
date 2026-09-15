using Bearcat.Domain.Entities;

namespace Bearcat.Domain.Shared.CollectionAssignment;

public record ReleaseUploadConfigMatch(
    UploadConfigTemplate UploadConfigTemplate,
    UploadConfig UploadConfig
);
