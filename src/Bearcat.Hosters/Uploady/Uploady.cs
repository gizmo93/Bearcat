using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Shared.XFilesharing;
using Bearcat.Hosters.Uploady.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Uploady;

public class Uploady(IUploadyApiClient apiClient, ILogger<Uploady> logger)
    : XFilesharingHosterBase<UploadyConfig>(apiClient, logger),
        IHosterWithDownload
{
    public override string Name => "uploady.io";

    protected override string FileUrlFormat => "https://uploady.io/{0}.html";

    public override bool SupportsPremiumOnlyDownloads => false;

    public bool DownloadRequiresPremium => false;
}
