using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.ProgressSteps;

public record ReleaseProgressStep(ReleaseProgressStepKind Kind, ReleaseProgressStepState State)
{
    public int Count { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<string> DatabaseNames { get; init; } = [];

    public ArchiveState? ArchiveProblemState { get; init; }
}
