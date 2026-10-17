using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Playmate.Api;
using Bearcat.Hosters.Shared.XVideoSharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Playmate;

public class Playmate(ApiClient apiClient, ILogger<Playmate> logger)
    : XVideoSharingHosterBase<PlaymateConfig>(apiClient, logger),
        IHosterWithFolders
{
    public override string Name => "Playmate";
}
