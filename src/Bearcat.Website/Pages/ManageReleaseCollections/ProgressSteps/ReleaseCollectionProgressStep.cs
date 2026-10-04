using Bearcat.Website.Shared.ProgressSteps;

namespace Bearcat.Website.Pages.ManageReleaseCollections.ProgressSteps;

public record ReleaseCollectionProgressStep(
    ReleaseCollectionProgressStepKind Kind,
    ProgressStepState State
)
{
    public int Count { get; init; }

    public int TotalCount { get; init; }

    public string? MetadataDatabaseName { get; init; }
}
