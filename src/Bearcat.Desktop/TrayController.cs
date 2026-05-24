using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Bearcat.Desktop;

public sealed class TrayController(TrayIcon trayIcon, NativeMenuItem statusMenuItem)
{
    private static readonly Uri RunningIconUri = new(
        "avares://Bearcat.Desktop/Assets/bearcat-tray-icon.png"
    );

    private static readonly Uri StoppedIconUri = new(
        "avares://Bearcat.Desktop/Assets/bearcat-tray-icon-stopped.png"
    );

    public TrayIcons Icons { get; } = new() { trayIcon };

    public void Update(TrayAppStatus status)
    {
        statusMenuItem.Header = $"Status: {status.ToDisplayText()}";
        trayIcon.ToolTipText = $"Bearcat - {status.ToDisplayText()}";

        var iconUri = status == TrayAppStatus.Running ? RunningIconUri : StoppedIconUri;
        using var iconStream = AssetLoader.Open(iconUri);
        trayIcon.Icon = new WindowIcon(iconStream);
    }
}
