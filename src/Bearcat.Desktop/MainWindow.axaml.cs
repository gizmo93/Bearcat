using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            if (settings.DatabaseProvider == DatabaseProvider.Postgres)
            {
                AppendLog(
                    "PostgreSQL validation uses the maintenance database, not the Bearcat database."
                );
            }

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
        WorkingDirectoriesTextBox.Text = string.Join(
            Environment.NewLine,
            settings.WorkingDirectories
        );
        RarPathTextBox.Text = settings.RarPath;
        SevenZipPathTextBox.Text = settings.SevenZipPath;
        BearcatHostPathTextBox.Text = settings.BearcatHostPath;
        SqliteRadioButton.IsChecked = settings.DatabaseProvider == DatabaseProvider.Sqlite;
        PostgresRadioButton.IsChecked = settings.DatabaseProvider == DatabaseProvider.Postgres;
        SqliteFilePathTextBox.Text = settings.SqliteFilePath;
        PostgresHostTextBox.Text = settings.PostgresHost;
        PostgresPortTextBox.Text = settings.PostgresPort.ToString(CultureInfo.InvariantCulture);
        PostgresDatabaseTextBox.Text = settings.PostgresDatabase;
        PostgresUsernameTextBox.Text = settings.PostgresUsername;
        PostgresPasswordTextBox.Text = settings.PostgresPassword;
        WebPortTextBox.Text = settings.WebPort.ToString(CultureInfo.InvariantCulture);
        ApiKeyTextBox.Text = settings.ApiKey;
        UpdateDatabaseSettingsVisibility();
    }

    private DatabaseProvider GetSelectedDatabaseProvider()
    {
        return SqliteRadioButton.IsChecked == true
            ? DatabaseProvider.Sqlite
            : DatabaseProvider.Postgres;
    }

    private void UpdateDatabaseSettingsVisibility()
    {
        var sqliteIsSelected = GetSelectedDatabaseProvider() == DatabaseProvider.Sqlite;
        SqliteSettingsGrid.IsVisible = sqliteIsSelected;
        PostgresSettingsGrid.IsVisible = !sqliteIsSelected;
    }

    private bool TryReadSettings(out DesktopSettings settings)
    {
        settings = new DesktopSettings
        {
            WorkingDirectories = ParseWorkingDirectories(WorkingDirectoriesTextBox.Text),
            RarPath = RarPathTextBox.Text?.Trim() ?? string.Empty,
            SevenZipPath = SevenZipPathTextBox.Text?.Trim() ?? string.Empty,
            BearcatHostPath = BearcatHostPathTextBox.Text?.Trim() ?? string.Empty,
            DatabaseProvider = GetSelectedDatabaseProvider(),
            SqliteFilePath = SqliteFilePathTextBox.Text?.Trim() ?? string.Empty,
            PostgresHost = PostgresHostTextBox.Text?.Trim() ?? string.Empty,
            PostgresDatabase = PostgresDatabaseTextBox.Text?.Trim() ?? string.Empty,
            PostgresUsername = PostgresUsernameTextBox.Text?.Trim() ?? string.Empty,
            PostgresPassword = PostgresPasswordTextBox.Text ?? string.Empty,
            ApiKey = ApiKeyTextBox.Text?.Trim() ?? string.Empty,
        };

        if (int.TryParse(PostgresPortTextBox.Text, out var postgresPort))
        {
            settings.PostgresPort = postgresPort;
        }
        else if (settings.DatabaseProvider == DatabaseProvider.Postgres)
        {
            AppendLog("Postgres port must be a number.");
            return false;
        }

        if (!int.TryParse(WebPortTextBox.Text, out var webPort))
        {
            AppendLog("Web port must be a number.");
            return false;
        }

        settings.WebPort = webPort;
        return true;
    }

    private static List<string> ParseWorkingDirectories(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Distinct()
            .ToList();
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
        TrayAppStatus status;

        if (isBusy)
        {
            status = TrayAppStatus.Working;
        }
        else if (hostProcess.IsRunning)
        {
            status = TrayAppStatus.Running;
        }
        else
        {
            status = TrayAppStatus.Stopped;
        }

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

    private async void BrowseWorkingDirectory_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Choose working directory");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var existing = WorkingDirectoriesTextBox.Text?.TrimEnd('\r', '\n') ?? string.Empty;
        WorkingDirectoriesTextBox.Text = string.IsNullOrWhiteSpace(existing)
            ? path
            : $"{existing}{Environment.NewLine}{path}";
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

    private void DatabaseProviderRadioButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        UpdateDatabaseSettingsVisibility();
    }

    private async void BrowseSqliteFilePath_Click(object? sender, RoutedEventArgs e)
    {
        var currentDirectory = Path.GetDirectoryName(SqliteFilePathTextBox.Text?.Trim());
        var file = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Choose SQLite database file",
                SuggestedFileName = "bearcat.db",
                DefaultExtension = "db",
                ShowOverwritePrompt = false,
                FileTypeChoices =
                [
                    new FilePickerFileType("SQLite database") { Patterns = ["*.db"] },
                ],
                SuggestedStartLocation = string.IsNullOrWhiteSpace(currentDirectory)
                    ? null
                    : await StorageProvider.TryGetFolderFromPathAsync(currentDirectory),
            }
        );
        var path = file?.TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(path))
        {
            SqliteFilePathTextBox.Text = path;
        }
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
