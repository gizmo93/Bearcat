using Bearcat.Hosters.Katfile.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Katfile;

public class Katfile(IKatfileApiClient apiClient, ILogger<Katfile> logger)
    : XFilesharingHosterBase<KatfileConfig>(apiClient, logger)
{
    public override string Name => "katfile.com";

    protected override string FileUrlFormat => "https://katfile.com/{0}.html";

    public override bool SupportsPremiumOnlyDownloads => false;

    protected override string? ExtractFileCode(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
        {
            return base.ExtractFileCode(fileUrl);
        }

        var pathSegments = uri
            .AbsolutePath.Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return pathSegments.Length switch
        {
            0 => null,
            1 => TrimHtmlExtension(pathSegments[0]),
            _ => pathSegments[0],
        };
    }

    private static string? TrimHtmlExtension(string value)
    {
        if (value.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^".html".Length];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
