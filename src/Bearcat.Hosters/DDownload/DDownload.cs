using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.DDownload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.DDownload;

public class DDownload(IDDownloadApiClient apiClient, ILogger<DDownload> logger)
    : XFilesharingHosterBase<DDownloadConfig>(apiClient, logger),
        IHosterWithDownload
{
    public override string Name => "ddownload";

    protected override string FileUrlFormat => "https://ddownload.com/{0}";

    public override bool SupportsPremiumOnlyDownloads => true;

    public bool DownloadRequiresPremium => false;
}
