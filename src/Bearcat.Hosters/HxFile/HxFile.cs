using Bearcat.Hosters.HxFile.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.HxFile;

public class HxFile(IHxFileApiClient apiClient, ILogger<HxFile> logger)
    : XFilesharingHosterBase<HxFileConfig>(apiClient, logger)
{
    public override string Name => "hxfile.co";

    protected override string FileUrlFormat => "https://hxfile.co/{0}.html";

    public override bool SupportsPremiumOnlyDownloads => false;
}
