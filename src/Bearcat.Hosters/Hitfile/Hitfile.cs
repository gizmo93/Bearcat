using Bearcat.Hosters.Shared.CostAction;
using Bearcat.Hosters.Shared.CostAction.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Hitfile;

public class Hitfile(ICostActionApiClient apiClient, ILogger<Hitfile> logger)
    : CostActionHosterBase<HitfileConfig>(apiClient, logger)
{
    public override string Name => "hitfile.net";

    public override IReadOnlyList<string> FileLinkHostNames => ["hitfile.net", "htfl.net"];

    protected override string AppType => "fd2";

    protected override string OwnFileLinkFormat => "https://htfl.net/{0}";
}
