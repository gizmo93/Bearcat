using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.DataVaults.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.DataVaults;

public class DataVaults(IDataVaultsApiClient apiClient, ILogger<DataVaults> logger)
    : XFilesharingHosterBase<DataVaultsConfig>(apiClient, logger),
        IHosterWithFileSizeLimit,
        IHosterWithDownload
{
    public override string Name => "datavaults.co";

    protected override string FileUrlFormat => "https://datavaults.co/{0}";

    public override bool SupportsPremiumOnlyDownloads => false;

    public bool DownloadRequiresPremium => false;

    protected override int MaximumParallelUploads => 5;

    // datavaults.co is VERY flaky!
    protected override int UploadRetryAttempts => 10;

    public int MaxFileSizeMb => 100;
}
