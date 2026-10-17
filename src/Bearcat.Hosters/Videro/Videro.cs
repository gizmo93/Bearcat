using Bearcat.Hosters.Shared.XVideoSharing;
using Bearcat.Hosters.Videro.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Videro;

public class Videro(ApiClient apiClient, ILogger<Videro> logger)
    : XVideoSharingHosterBase<VideroConfig>(apiClient, logger)
{
    public override string Name => "Videro";
}
