namespace Bearcat.Domain.UseCases.PostToForums.Models;

public enum AutoPostResultStatus
{
    Posted = 1,
    Updated = 2,
    Skipped = 3,
    Failed = 4,
    ThreadSelectionNeeded = 5,
}
