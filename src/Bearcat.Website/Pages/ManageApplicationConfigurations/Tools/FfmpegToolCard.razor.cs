using Bearcat.Abstractions.Media;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageApplicationConfigurations.Tools;

public sealed partial class FfmpegToolCard(IFfmpegInstaller ffmpegInstaller)
    : ComponentBase,
        IDisposable
{
    private static readonly TimeSpan InstallStateRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource lifetimeCancellation = new();

    private FfmpegStatus? status;

    private bool isWatchingInstallState;

    private FfmpegInstallState installState = new(IsRunning: false, LastErrorMessage: null);

    private bool CanDownload =>
        status?.Source is FfmpegExecutableSource.Installed or FfmpegExecutableSource.Missing;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        installState = ffmpegInstaller.GetInstallState();
        status = await ffmpegInstaller.GetStatusAsync(lifetimeCancellation.Token);
        StateHasChanged();

        if (installState.IsRunning)
        {
            await WatchInstallStateAsync(installTask: null);
        }
    }

    private async Task DownloadAsync()
    {
        var installTask = Task.Run(() =>
            ffmpegInstaller.InstallLatestAsync(CancellationToken.None)
        );

        await WatchInstallStateAsync(installTask);
    }

    private async Task WatchInstallStateAsync(Task? installTask)
    {
        using var refreshTimer = new PeriodicTimer(InstallStateRefreshInterval);
        isWatchingInstallState = true;

        try
        {
            do
            {
                installState = ffmpegInstaller.GetInstallState();
                StateHasChanged();
                await refreshTimer.WaitForNextTickAsync(lifetimeCancellation.Token);
            } while (installState.IsRunning || installTask is { IsCompleted: false });

            installState = ffmpegInstaller.GetInstallState();
            status = await ffmpegInstaller.GetStatusAsync(lifetimeCancellation.Token);
            StateHasChanged();
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested) { }
        finally
        {
            isWatchingInstallState = false;
        }
    }

    public void Dispose()
    {
        lifetimeCancellation.Cancel();
        lifetimeCancellation.Dispose();
    }
}
