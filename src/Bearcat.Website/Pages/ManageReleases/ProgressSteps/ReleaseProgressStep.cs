using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Shared.ProgressSteps;

namespace Bearcat.Website.Pages.ManageReleases.ProgressSteps;

public record ReleaseProgressStep(ReleaseProgressStepKind Kind, ProgressStepState State)
{
    public int Count { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<string> DatabaseNames { get; init; } = [];

    public ArchiveState? ArchiveProblemState { get; init; }
}
