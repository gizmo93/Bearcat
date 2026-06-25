using Bearcat.Hosters.FileUpload.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.FileUpload;

public class FileUpload(IFileUploadApiClient apiClient, ILogger<FileUpload> logger)
    : XFilesharingHosterBase<FileUploadConfig>(apiClient, logger)
{
    public override string Name => "file-upload.org";

    protected override string FileUrlFormat => "https://file-upload.org/{0}";

    public override bool SupportsPremiumOnlyDownloads => false;

    protected override int MaximumParallelUploads => 10;
}
