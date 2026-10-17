using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.DoodStream.Api;
using Bearcat.Hosters.Shared.XVideoSharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.DoodStream;

public class DoodStream(ApiClient apiClient, ILogger<DoodStream> logger)
    : XVideoSharingHosterBase<DoodStreamConfig>(apiClient, logger),
        IHosterWithFolders
{
    public override string Name => "DoodStream";
}
