using Bearcat.Hosters.FileQ.Api;
using Bearcat.Hosters.Shared.XFilesharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.FileQ;

public class FileQ(IFileQApiClient apiClient, ILogger<FileQ> logger)
    : XFilesharingHosterBase<FileQConfig>(apiClient, logger)
{
    public override string Name => "fileq.net";

    protected override string FileUrlFormat => "https://fileq.net/{0}";

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
