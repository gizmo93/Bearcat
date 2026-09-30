using Microsoft.Playwright;

namespace Bearcat.DistributionSites.Shared;

internal static class PlaywrightBrowsers
{
    private const string BrowsersPathVariable = "PLAYWRIGHT_BROWSERS_PATH";
    private const string BundledFolderName = "playwright-browsers";

    private static readonly Lock InstallLock = new();
    private static bool chromiumIsInstalled;

    public static void InstallChromiumIfMissing()
    {
        lock (InstallLock)
        {
            if (chromiumIsInstalled)
            {
                return;
            }

            var browsersPath = GetBrowsersPath();
            Environment.SetEnvironmentVariable(BrowsersPathVariable, browsersPath);

            if (!HasChromium(browsersPath))
            {
                Directory.CreateDirectory(browsersPath);
                var exitCode = Program.Main(["install", "chromium"]);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Playwright Chromium install failed with exit code {exitCode}."
                    );
                }
            }

            chromiumIsInstalled = true;
        }
    }

    private static string GetBrowsersPath()
    {
        var configured = Environment.GetEnvironmentVariable(BrowsersPathVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, BundledFolderName);
        if (HasChromium(bundled))
        {
            return bundled;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bearcat",
            BundledFolderName
        );
    }

    private static bool HasChromium(string browsersPath)
    {
        return Directory.Exists(browsersPath)
            && Directory.EnumerateDirectories(browsersPath, "chromium*").Any();
    }
}
