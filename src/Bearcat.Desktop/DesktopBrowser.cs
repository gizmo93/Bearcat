using System;
using System.Diagnostics;

namespace Bearcat.Desktop;

public static class DesktopBrowser
{
    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            var fallback =
                OperatingSystem.IsMacOS() ? new ProcessStartInfo("open", url)
                : OperatingSystem.IsWindows() ? new ProcessStartInfo("cmd", $"/c start {url}")
                : new ProcessStartInfo("xdg-open", url);

            Process.Start(fallback);
        }
    }
}
