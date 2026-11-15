using System.Globalization;
using Bearcat.Abstractions.Configurations;
using Microsoft.Extensions.Configuration;

namespace Bearcat.Domain.UseCases.ManageReleaseScreenshots.Storage;

public class ReleaseScreenshotDirectory(IConfiguration configuration)
{
    private const string ScreenshotsFolderName = "screenshots";

    public string GetDirectoryPath(int releaseId)
    {
        return Path.Combine(
            BearcatDataDirectory.Resolve(configuration),
            ScreenshotsFolderName,
            releaseId.ToString(CultureInfo.InvariantCulture)
        );
    }

    public static string GetRelativeFilePath(int releaseId, string fileName)
    {
        return $"{ScreenshotsFolderName}/{releaseId.ToString(CultureInfo.InvariantCulture)}/{fileName}";
    }

    public string GetFilePath(string relativeFilePath)
    {
        return Path.Combine(BearcatDataDirectory.Resolve(configuration), relativeFilePath);
    }
}
