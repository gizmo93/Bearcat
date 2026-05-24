using Bearcat.Hosters.DDownload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.DDownload;

public class DDownload(IDDownloadApiClient apiClient, ILogger<DDownload> logger)
    : XFilesharingHosterBase<DDownloadConfig>(apiClient, logger)
{
    public override string Name => "ddownload";

    protected override string FileUrlFormat => "https://www.ddownload.com/{0}";
}
