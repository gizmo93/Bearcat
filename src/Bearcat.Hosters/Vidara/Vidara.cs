using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Shared.XVideoSharing;
using Bearcat.Hosters.Vidara.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Vidara;

public class Vidara(ApiClient apiClient, ILogger<Vidara> logger)
    : XVideoSharingHosterBase<VidaraConfig>(apiClient, logger),
        IHosterWithFolders
{
    public override string Name => "Vidara";

    public override IReadOnlyList<string> FileLinkHostNames =>
        ["vidara.to", "vidara.so", "vidaraa.cc", "streamix.so", "stmix.io"];
}
