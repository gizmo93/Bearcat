using Bearcat.Hosters.FileServe.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.FileServe;

public class FileServe(IFileServeApiClient apiClient, ILogger<FileServe> logger)
    : XFilesharingHosterBase<FileServeConfig>(apiClient, logger)
{
    public override string Name => "fileserve.com";

    protected override string FileUrlFormat => "https://fileserve.com/{0}";

    public override bool SupportsPremiumOnlyDownloads => false;
}
