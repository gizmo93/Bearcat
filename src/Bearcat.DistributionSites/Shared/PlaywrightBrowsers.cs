using Microsoft.Playwright;

namespace Bearcat.DistributionSites.Shared;

internal static class PlaywrightBrowsers
{
    private const string BrowsersPathVariable = "PLAYWRIGHT_BROWSERS_PATH";
    private const string BundledFolderName = "playwright-browsers";

    private static readonly Lock Gate = new();
    private static bool ready;

    public static void Ensure()
    {
        lock (Gate)
        {
            if (ready)
            {
                return;
            }

            var browsersPath = ResolveBrowsersPath();
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

            ready = true;
        }
    }

    private static string ResolveBrowsersPath()
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
