using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.Mega4Upload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Mega4Upload;

public class Mega4Upload(IMega4UploadApiClient apiClient, ILogger<Mega4Upload> logger)
    : XFilesharingHosterBase<Mega4UploadConfig>(apiClient, logger),
        IHosterWithDownload
{
    public override string Name => "mega4upload.net";

    protected override string FileUrlFormat => "https://mega4upload.net/{0}.html";

    public override bool SupportsPremiumOnlyDownloads => false;

    public bool DownloadRequiresPremium => true;
}
