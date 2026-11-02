using Bearcat.Hosters.ClicknUpload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.ClicknUpload;

public class ClicknUpload(IClicknUploadApiClient apiClient, ILogger<ClicknUpload> logger)
    : XFilesharingHosterBase<ClicknUploadConfig>(apiClient, logger)
{
    public override string Name => "clicknupload.click";

    public override IReadOnlyList<string> FileLinkHostNames =>
        [
            "clicknupload.click",
            "clicknupload.link",
            "clicknupload.red",
            "clicknupload.to",
            "clicknupload.cc",
            "clicknupload.co",
            "clicknupload.org",
            "clicknupload.com",
            "clicknupload.me",
            "clicknupload.club",
            "clicknupload.online",
            "clicknupload.download",
            "clicknupload.vip",
            "clicknupload.site",
            "clicknupload.xyz",
            "clicknupload.one",
            "clicknupload.name",
            "clicknupload.space",
            "clickndownload.org",
            "clickndownload.space",
            "clickndownload.click",
            "clickndownload.link",
            "clickndownload.site",
            "clickndownload.online",
            "clicknupload.cfd",
            "clickndownload.cloud",
            "clickndownload.cfd",
        ];

    protected override string FileUrlFormat => "https://clicknupload.click/{0}";

    public override bool SupportsPremiumOnlyDownloads => false;
}
