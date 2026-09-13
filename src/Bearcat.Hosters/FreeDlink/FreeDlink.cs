using Bearcat.Hosters.FreeDlink.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.FreeDlink;

public class FreeDlink(IFreeDlinkApiClient apiClient, ILogger<FreeDlink> logger)
    : XFilesharingHosterBase<FreeDlinkConfig>(apiClient, logger)
{
    public override string Name => "freedl.ink";

    protected override string FileUrlFormat => "https://freedl.ink/{0}.html";

    public override bool SupportsPremiumOnlyDownloads => false;
}
