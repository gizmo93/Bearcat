using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.CollectionAssignment;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;

public record ReleaseFromTemplateData(
    Release Release,
    IReadOnlyList<ReleaseUploadConfigMatch> UploadConfigMatches
);
