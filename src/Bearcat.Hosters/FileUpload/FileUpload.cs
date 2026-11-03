using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.FileUpload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.FileUpload;

public class FileUpload(IFileUploadApiClient apiClient, ILogger<FileUpload> logger)
    : XFilesharingHosterBase<FileUploadConfig>(apiClient, logger),
        IHosterWithDownload
{
    public override string Name => "file-upload.org";

    public override IReadOnlyList<string> FileLinkHostNames =>
        [
            "file-upload.org",
            "file-up.org",
            "file-up.io",
            "file-up.cc",
            "file-up.com",
            "file-upload.io",
            "file-upload.cc",
            "file-upload.com",
            "file-upload.download",
        ];

    protected override string FileUrlFormat => "https://file-upload.org/{0}";

    public override bool SupportsPremiumOnlyDownloads => false;

    public bool DownloadRequiresPremium => false;

    protected override int MaximumParallelUploads => 10;
}
