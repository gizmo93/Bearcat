namespace Bearcat.Domain.UseCases.PostToForums;

public sealed record AutoPostRunResult(int PostedCount, IReadOnlyList<AutoPostRunFailure> Failures)
{
    public int FailedCount => Failures.Count;
}
