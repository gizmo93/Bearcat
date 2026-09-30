namespace Bearcat.Abstractions.Transfers;

public static class TransferSpeedLimitScope
{
    private static readonly AsyncLocal<IReadOnlyList<TransferSpeedLimiter>?> CurrentLimiters =
        new();

    public static IReadOnlyList<TransferSpeedLimiter>? Current => CurrentLimiters.Value;

    public static IDisposable Enter(IReadOnlyList<TransferSpeedLimiter> limiters)
    {
        var previousLimiters = CurrentLimiters.Value;
        CurrentLimiters.Value = limiters.Count == 0 ? null : limiters;

        return new TransferSpeedLimitScopeExit(previousLimiters);
    }

    private sealed class TransferSpeedLimitScopeExit(
        IReadOnlyList<TransferSpeedLimiter>? previousLimiters
    ) : IDisposable
    {
        public void Dispose()
        {
            CurrentLimiters.Value = previousLimiters;
        }
    }
}
