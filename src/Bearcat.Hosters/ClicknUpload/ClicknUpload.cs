using Bearcat.Hosters.ClicknUpload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.ClicknUpload;

public class ClicknUpload(IClicknUploadApiClient apiClient, ILogger<ClicknUpload> logger)
    : XFilesharingHosterBase<ClicknUploadConfig>(apiClient, logger)
{
    public override string Name => "clicknupload.click";

    protected override string FileUrlFormat => "https://clicknupload.click/{0}";

    public override bool SupportsPremiumOnlyDownloads => false;
}
