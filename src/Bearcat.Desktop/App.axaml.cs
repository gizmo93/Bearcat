using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Bearcat.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MacDockIcon.Set("Assets/bearcat-icon.png");
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settingsStore = new DesktopSettingsStore();
            var hostProcess = new BearcatHostProcess();
            var mainWindow = new MainWindow(settingsStore, hostProcess, desktop);
            var trayController = CreateTrayController(mainWindow);
            mainWindow.SetTrayStatusUpdater(trayController.Update);

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => hostProcess.Dispose();
            TrayIcon.SetIcons(this, trayController.Icons);
            trayController.Update(TrayAppStatus.Stopped);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static TrayController CreateTrayController(MainWindow window)
    {
        var statusMenuItem = new NativeMenuItem { Header = "Status: Stopped", IsEnabled = false };
        var menu = new NativeMenu
        {
            Items =
            {
                statusMenuItem,
                new NativeMenuItemSeparator(),
                new NativeMenuItem
                {
                    Header = "Open Bearcat",
                    Command = new AsyncCommand(_ => window.OpenBearcatAsync()),
                },
                new NativeMenuItem
                {
                    Header = "Show Settings",
                    Command = new RelayCommand(_ => window.ShowAndActivate()),
                },
                new NativeMenuItem
                {
                    Header = "Start",
                    Command = new AsyncCommand(_ => window.StartBearcatAsync()),
                },
                new NativeMenuItem
                {
                    Header = "Stop",
                    Command = new AsyncCommand(_ => window.StopBearcatAsync()),
                },
                new NativeMenuItemSeparator(),
                new NativeMenuItem
                {
                    Header = "Quit Bearcat",
                    Command = new AsyncCommand(_ => window.QuitAsync()),
                },
            },
        };

        var trayIcon = new TrayIcon { ToolTipText = "Bearcat", Menu = menu };
        MacOSProperties.SetIsTemplateIcon(trayIcon, false);

        return new TrayController(trayIcon, statusMenuItem);
    }
}
