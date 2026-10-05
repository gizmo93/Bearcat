namespace Bearcat.Website.Shared.ProgressSteps;

public record ProgressStepDisplay<TKind>(
    TKind Kind,
    ProgressStepState State,
    string Title,
    string ShortTitle,
    string Description,
    string IconName
);
