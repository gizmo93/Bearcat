namespace Bearcat.Domain.UseCases.PostToForums.Models;

public sealed record AutoPostRunResult(
    int PostedCount,
    int UpdatedCount,
    IReadOnlyList<AutoPostRunFailure> Failures
)
{
    public int FailedCount => Failures.Count;
}
