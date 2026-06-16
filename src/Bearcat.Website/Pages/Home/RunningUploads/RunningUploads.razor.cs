using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.ValueObjects;
using BlazorBlueprint.Components;
using Humanizer;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.Home.RunningUploads;

public partial class RunningUploads(
    DialogService dialogService,
    ToastService toastService,
    IServiceScopeFactory serviceScopeFactory
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Upload> Uploads { get; set; } = null!;

    [Parameter]
    public IReadOnlyDictionary<int, double> UploadSpeeds { get; set; } =
        new Dictionary<int, double>();

    [Parameter]
    public EventCallback OnUploadCanceled { get; set; }

    private readonly HashSet<int> showDetailIds = [];

    private IEnumerable<Upload> SortedUploads => Uploads.OrderByDescending(u => u.UploadState);

    private IReadOnlyList<Upload> ExpandedUploads =>
        SortedUploads.Where(upload => showDetailIds.Contains(upload.Id)).ToList();

    private void ToggleShowUploadDetails(int uploadId)
    {
        if (!showDetailIds.Remove(uploadId))
        {
            showDetailIds.Add(uploadId);
        }

        StateHasChanged();
    }

    private static BadgeVariant GetUploadVariant(UploadState state) =>
        state switch
        {
            UploadState.Uploading => BadgeVariant.Default,
            UploadState.CancellationRequested => BadgeVariant.Secondary,
            UploadState.Pending => BadgeVariant.Secondary,
            UploadState.Failed => BadgeVariant.Destructive,
            _ => BadgeVariant.Outline,
        };

    private async Task CancelUploadAsync(Upload upload)
    {
        var result = await dialogService.ConfirmAsync(
            L["CancelUpload"],
            L["CancelUploadConfirmation", upload.Id],
            new ConfirmDialogOptions
            {
                ConfirmText = L["CancelUpload"],
                CancelText = L["Close"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var uploadStateService = scope.ServiceProvider.GetRequiredService<UploadStateService>();
        var cancellationRequested = await uploadStateService.CancelUploadAsync(upload.Id);

        if (cancellationRequested)
        {
            toastService.Success(L["UploadCancellationRequested", upload.Id]);
            await OnUploadCanceled.InvokeAsync();
        }
        else
        {
            toastService.Error(L["UploadCancellationNotAvailable", upload.Id]);
        }
    }

    private static bool CanCancelUpload(Upload upload) =>
        upload.UploadState is UploadState.Pending or UploadState.Uploading;

    private static double GetUploadProgress(Upload upload)
    {
        var uploadedFiles = upload.UploadedFiles.Count;
        var archiveFiles = upload.Archive?.ArchiveFiles.Count ?? 0;

        if (archiveFiles == 0)
        {
            return 0;
        }

        return Math.Round((double)uploadedFiles / archiveFiles * 100, 0);
    }

    private double TotalUploadBytesPerSecond =>
        UploadSpeeds.Values.Where(bytesPerSecond => bytesPerSecond > 0).Sum();

    private string? FormatUploadSpeed(int uploadId)
    {
        return UploadSpeeds.TryGetValue(uploadId, out var bytesPerSecond)
            ? FormatSpeed(bytesPerSecond)
            : null;
    }

    private static string? FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond <= 0 ? null : $"{bytesPerSecond.Bytes().Humanize("0.0")}/s";
    }
}
