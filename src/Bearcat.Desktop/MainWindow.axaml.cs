using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Npgsql;

namespace Bearcat.Desktop;

public partial class MainWindow : Window
{
    private readonly DesktopSettingsStore settingsStore = null!;
    private readonly BearcatHostProcess hostProcess = null!;
    private readonly IClassicDesktopStyleApplicationLifetime desktopLifetime = null!;
    private Action<TrayAppStatus>? updateTrayStatus;
    private bool isBusy;
    private bool isQuitting;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(
        DesktopSettingsStore settingsStore,
        BearcatHostProcess hostProcess,
        IClassicDesktopStyleApplicationLifetime desktopLifetime
    )
    {
        this.settingsStore = settingsStore;
        this.hostProcess = hostProcess;
        this.desktopLifetime = desktopLifetime;

        InitializeComponent();

        LoadSettings(settingsStore.Load());
        AppendLog($"Bearcat.Desktop loaded from {AppContext.BaseDirectory}.");
        hostProcess.LogReceived += line => Dispatcher.UIThread.Post(() => AppendLog(line));
        hostProcess.Exited += code =>
            Dispatcher.UIThread.Post(() =>
            {
                AppendLog($"Bearcat.Host exited with code {code}.");
                UpdateStatus();
            });

        UpdateStatus();
    }

    public void SetTrayStatusUpdater(Action<TrayAppStatus> updater)
    {
        updateTrayStatus = updater;
        UpdateStatus();
    }

    public void ShowAndActivate()
    {
        MacDockVisibility.Show();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public async Task StartBearcatAsync()
    {
        if (isBusy || hostProcess.IsRunning)
        {
            UpdateStatus();
            return;
        }

        if (!TryReadSettings(out var settings))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            settingsStore.Save(settings);
            AppendLog("Validating settings...");
            AppendLog(
                "PostgreSQL validation uses the maintenance database, not the Bearcat database."
            );
            await DesktopSettingsValidator.ValidateAsync(settings);

            AppendLog("Starting Bearcat.Host...");
            await hostProcess.StartAsync(settings);
            AppendLog($"Bearcat is running at {settings.WebUrl}.");
            UpdateStatus();
        });
    }

    public async Task StopBearcatAsync()
    {
        if (isBusy)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            AppendLog("Stopping Bearcat.Host...");
            await hostProcess.StopAsync();
            UpdateStatus();
        });
    }

    public async Task OpenBearcatAsync()
    {
        if (!hostProcess.IsRunning)
        {
            await StartBearcatAsync();
        }

        if (TryReadSettings(out var settings))
        {
            DesktopBrowser.Open(settings.WebUrl);
        }
    }

    public async Task QuitAsync()
    {
        isQuitting = true;
        await StopBearcatAsync();
        desktopLifetime.Shutdown();
    }

    private void LoadSettings(DesktopSettings settings)
    {
        ReleasePathTextBox.Text = settings.ReleaseDataDirectory;
        RarPathTextBox.Text = settings.RarPath;
        SevenZipPathTextBox.Text = settings.SevenZipPath;
        BearcatHostPathTextBox.Text = settings.BearcatHostPath;
        PostgresHostTextBox.Text = settings.PostgresHost;
        PostgresPortTextBox.Text = settings.PostgresPort.ToString();
        PostgresDatabaseTextBox.Text = settings.PostgresDatabase;
        PostgresUsernameTextBox.Text = settings.PostgresUsername;
        PostgresPasswordTextBox.Text = settings.PostgresPassword;
        WebPortTextBox.Text = settings.WebPort.ToString();
    }

    private bool TryReadSettings(out DesktopSettings settings)
    {
        settings = new DesktopSettings
        {
            ReleaseDataDirectory = ReleasePathTextBox.Text?.Trim() ?? string.Empty,
            RarPath = RarPathTextBox.Text?.Trim() ?? string.Empty,
            SevenZipPath = SevenZipPathTextBox.Text?.Trim() ?? string.Empty,
            BearcatHostPath = BearcatHostPathTextBox.Text?.Trim() ?? string.Empty,
            PostgresHost = PostgresHostTextBox.Text?.Trim() ?? string.Empty,
            PostgresDatabase = PostgresDatabaseTextBox.Text?.Trim() ?? string.Empty,
            PostgresUsername = PostgresUsernameTextBox.Text?.Trim() ?? string.Empty,
            PostgresPassword = PostgresPasswordTextBox.Text ?? string.Empty,
        };

        if (!int.TryParse(PostgresPortTextBox.Text, out var postgresPort))
        {
            AppendLog("Postgres port must be a number.");
            return false;
        }

        if (!int.TryParse(WebPortTextBox.Text, out var webPort))
        {
            AppendLog("Web port must be a number.");
            return false;
        }

        settings.PostgresPort = postgresPort;
        settings.WebPort = webPort;
        return true;
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        isBusy = true;
        UpdateStatus();

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog(GetLogMessage(ex));
        }
        finally
        {
            isBusy = false;
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        var status =
            isBusy ? TrayAppStatus.Working
            : hostProcess.IsRunning ? TrayAppStatus.Running
            : TrayAppStatus.Stopped;

        StatusTextBlock.Text = status.ToDisplayText();
        updateTrayStatus?.Invoke(status);

        StartButton.IsEnabled = !isBusy && !hostProcess.IsRunning;
        StopButton.IsEnabled = !isBusy && hostProcess.IsRunning;
        OpenButton.IsEnabled = !isBusy;
        SaveButton.IsEnabled = !isBusy;
        QuitButton.IsEnabled = !isBusy;
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogTextBox.Text = string.IsNullOrWhiteSpace(LogTextBox.Text)
            ? line
            : $"{LogTextBox.Text}{Environment.NewLine}{line}";

        if (LogTextBox.Text.Length > 20000)
        {
            LogTextBox.Text = LogTextBox.Text[^20000..];
        }

        LogTextBox.CaretIndex = LogTextBox.Text.Length;
    }

    private static string GetLogMessage(Exception exception)
    {
        if (exception is PostgresException postgresException)
        {
            return $"PostgreSQL rejected the connection with SQL state {postgresException.SqlState}.";
        }

        return exception.Message;
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TryReadSettings(out var settings))
        {
            settingsStore.Save(settings);
            AppendLog($"Saved settings to {settingsStore.SettingsPath}.");
        }
    }

    private async void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        await StartBearcatAsync();
    }

    private async void StopButton_Click(object? sender, RoutedEventArgs e)
    {
        await StopBearcatAsync();
    }

    private async void OpenButton_Click(object? sender, RoutedEventArgs e)
    {
        await OpenBearcatAsync();
    }

    private async void QuitButton_Click(object? sender, RoutedEventArgs e)
    {
        await QuitAsync();
    }

    private async void BrowseReleasePath_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Choose release data directory");
        if (!string.IsNullOrWhiteSpace(path))
        {
            ReleasePathTextBox.Text = path;
        }
    }

    private async void BrowseRarPath_Click(object? sender, RoutedEventArgs e)
    {
        await PickFileIntoTextBoxAsync(RarPathTextBox, "Choose RAR executable");
    }

    private async void BrowseSevenZipPath_Click(object? sender, RoutedEventArgs e)
    {
        await PickFileIntoTextBoxAsync(SevenZipPathTextBox, "Choose 7z executable");
    }

    private async void BrowseBearcatHostPath_Click(object? sender, RoutedEventArgs e)
    {
        await PickFileIntoTextBoxAsync(BearcatHostPathTextBox, "Choose Bearcat.Host executable");
    }

    private async Task PickFileIntoTextBoxAsync(TextBox textBox, string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { AllowMultiple = false, Title = title }
        );
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;

        if (!string.IsNullOrWhiteSpace(path))
        {
            textBox.Text = path;
        }
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, Title = title }
        );

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (isQuitting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        MacDockVisibility.Hide();
    }
}
