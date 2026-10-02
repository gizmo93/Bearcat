using Microsoft.Extensions.Configuration;

namespace Bearcat.Infrastructure.Configuration;

public static class BearcatDataDirectory
{
    public static string Resolve(IConfiguration configuration)
    {
        var dataDirectory =
            Environment.GetEnvironmentVariable("BEARCAT_DATA_DIR")
            ?? configuration["Bearcat:DataDirectory"];

        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = IsRunningInContainer()
                ? "/data"
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Bearcat"
                );
        }

        return Path.GetFullPath(dataDirectory);
    }

    private static bool IsRunningInContainer()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
    }
}
