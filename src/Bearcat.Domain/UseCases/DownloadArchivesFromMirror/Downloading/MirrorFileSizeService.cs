using Bearcat.Domain.Shared.Proxies;
using Microsoft.Extensions.Logging;

namespace Bearcat.Domain.UseCases.DownloadArchivesFromMirror.Downloading;

public class MirrorFileSizeService(ILogger<MirrorFileSizeService> logger)
{
    public async Task<IReadOnlyDictionary<string, long>> GetFileSizesOrEmptyAsync(
        ResolvedMirrorHoster resolvedMirrorHoster,
        IReadOnlyList<string> hosterFileLinks,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using var proxyScope = HosterRegistrationProxyScope.EnterForMirrorDownload(
                resolvedMirrorHoster.Registration
            );

            return await resolvedMirrorHoster.Hoster.GetFileSizesAsync(
                hosterFileLinks,
                resolvedMirrorHoster.Config,
                cancellationToken
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Could not determine the size of {FileCount} mirror files on hoster {HosterName}, the download progress stays indeterminate",
                hosterFileLinks.Count,
                resolvedMirrorHoster.Registration.Name
            );

            return new Dictionary<string, long>();
        }
    }
}
