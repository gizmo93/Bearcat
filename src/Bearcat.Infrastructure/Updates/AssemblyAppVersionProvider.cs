using System.Reflection;
using Bearcat.Abstractions.Updates;

namespace Bearcat.Infrastructure.Updates;

public sealed class AssemblyAppVersionProvider : IAppVersionProvider
{
    private const string DevelopmentVersion = "0.0.0-dev";

    public string CurrentVersion { get; } = ReadCurrentVersion();

    private static string ReadCurrentVersion()
    {
        var informationalVersion = Assembly
            .GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return DevelopmentVersion;
        }

        // The SDK appends "+<git-sha>" build metadata that we do not want to display or compare.
        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }
}
