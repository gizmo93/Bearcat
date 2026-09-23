using Bearcat.Hosters.Shared.CostAction;
using Bearcat.Hosters.Shared.CostAction.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Turbobit;

public class Turbobit(ICostActionApiClient apiClient, ILogger<Turbobit> logger)
    : CostActionHosterBase<TurbobitConfig>(apiClient, logger)
{
    public override string Name => "turbobit.net";

    protected override string AppType => "fd1";
}
