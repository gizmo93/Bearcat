using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ManageReleases;

public record ReleaseFromTemplateData(
    Release Release,
    IReadOnlyList<ReleaseUploadConfigMatch> UploadConfigMatches
);
