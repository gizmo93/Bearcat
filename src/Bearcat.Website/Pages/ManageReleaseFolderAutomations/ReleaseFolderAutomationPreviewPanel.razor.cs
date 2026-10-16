using Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleaseFolderAutomations;

public partial class ReleaseFolderAutomationPreviewPanel(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    private const int MaxPreviewFolderCount = 50;

    [Parameter]
    public string BasePath { get; set; } = string.Empty;

    [Parameter]
    public string? FolderNamePattern { get; set; }

    [Parameter]
    public bool ShowInitialScanOutcome { get; set; }

    [Parameter]
    public bool IgnoreExistingOnFirstScan { get; set; }

    [Parameter]
    public EventCallback<ReleaseFolderAutomationPreview?> OnPreviewLoaded { get; set; }

    private ReleaseFolderAutomationPreview? preview;
    private bool isLoadingPreview;
    private int previewRequestNumber;
    private string? loadedBasePath;
    private string? loadedFolderNamePattern;

    protected override async Task OnParametersSetAsync()
    {
        if (BasePath == loadedBasePath && FolderNamePattern == loadedFolderNamePattern)
        {
            return;
        }

        await RefreshPreviewAsync();
    }

    private async Task RefreshPreviewAsync()
    {
        var requestNumber = ++previewRequestNumber;
        var basePath = BasePath;
        var folderNamePattern = FolderNamePattern;

        loadedBasePath = basePath;
        loadedFolderNamePattern = folderNamePattern;

        if (string.IsNullOrWhiteSpace(basePath))
        {
            preview = null;
            isLoadingPreview = false;
            await OnPreviewLoaded.InvokeAsync(null);
            return;
        }

        isLoadingPreview = true;

        var result = await operationRunner.RunAsync(
            (ReleaseFolderAutomationPreviewService service) =>
                service.PreviewMatchingFoldersAsync(basePath, folderNamePattern)
        );

        if (requestNumber != previewRequestNumber)
        {
            return;
        }

        preview = result;
        isLoadingPreview = false;
        await OnPreviewLoaded.InvokeAsync(result);
    }
}
