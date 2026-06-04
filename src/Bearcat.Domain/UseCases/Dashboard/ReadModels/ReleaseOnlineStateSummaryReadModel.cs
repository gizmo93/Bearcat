using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.Dashboard.ReadModels;

public sealed record ReleaseOnlineStateSummaryReadModel(
    int TotalReleaseCount,
    IReadOnlyList<ReleaseOnlineStateCountReadModel> Counts
);

public sealed record ReleaseOnlineStateCountReadModel(OnlineState OnlineState, int ReleaseCount);
