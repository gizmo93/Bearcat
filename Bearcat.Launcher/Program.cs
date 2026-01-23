using Avalonia;
using System;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Bearcat.Frontend;

namespace Bearcat.Launcher;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = BuildAvaloniaApp(args);
        var app = builder.Instance;;
        AppMain(app!, args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp(string[] args)
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                ShowInDock = false,
                DisableDefaultApplicationMenuItems = true,
            })
            .WithInterFont()
            .SetupWithClassicDesktopLifetime(args, options =>
            {
                options.MainWindow = null;
                options.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            })
            .LogToTrace();
    }
    
    private static void AppMain(Application app, string[] args)
    {
        // A cancellation token source that will be 
        // used to stop the main loop
        var cts = new CancellationTokenSource();

        Startup.StartupAsync(args);
        
        // Start the main loop
        app.Run(cts.Token);
    }
}
