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
            ProcessStartInfo fallback;

            if (OperatingSystem.IsMacOS())
            {
                fallback = new ProcessStartInfo("open", url);
            }
            else if (OperatingSystem.IsWindows())
            {
                fallback = new ProcessStartInfo("cmd", $"/c start {url}");
            }
            else
            {
                fallback = new ProcessStartInfo("xdg-open", url);
            }

            Process.Start(fallback);
        }
    }
}
