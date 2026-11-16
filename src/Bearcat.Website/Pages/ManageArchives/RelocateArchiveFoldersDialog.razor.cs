using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ManageArchives.ArchiveFolderRelocations;
using Bearcat.Domain.UseCases.ManageArchiveStorageFolders.Repositories;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageArchives;

public partial class RelocateArchiveFoldersDialog(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
) : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<int> ArchiveIds { get; set; } = [];

    private IReadOnlyList<string> baseFolderPaths = [];
    private string? basePath;
    private IReadOnlyList<ArchiveFolderRelocationPreviewItem>? previewItems;
    private bool isLoadingPreview;
    private bool isRelocating;

    private int RelocatableArchiveCount =>
        previewItems?.Count(previewItem => previewItem.IsRelocatable) ?? 0;

    private bool CanRelocate => !isLoadingPreview && !isRelocating && RelocatableArchiveCount > 0;

    protected override async Task OnInitializedAsync()
    {
        var archiveStorageFolders = await operationRunner.RunAsync(
            (IArchiveStorageFolderReadRepository repository) => repository.GetAllAsync()
        );

        baseFolderPaths = workingDirectoriesConfig
            .Value.GetWorkingDirectories()
            .Concat(archiveStorageFolders.Select(archiveStorageFolder => archiveStorageFolder.Path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task SelectBasePathAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] = baseFolderPaths,
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = basePath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectArchiveFolderSearchBasePath"],
                Description = L["SelectArchiveFolderSearchBasePathDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var selectedPath = result.GetData<string>();

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        basePath = selectedPath;
        await LoadPreviewAsync();
    }

    private async Task LoadPreviewAsync()
    {
        var selectedBasePath = basePath!;
        previewItems = null;
        isLoadingPreview = true;
        StateHasChanged();

        try
        {
            var loadedPreviewItems = await Task.Run(() =>
                operationRunner.RunAsync(
                    (ArchiveFolderRelocationService service) =>
                        service.GetRelocationPreviewAsync(
                            archiveIds: ArchiveIds,
                            basePath: selectedBasePath,
                            cancellationToken: CancellationToken.None
                        )
                )
            );

            previewItems = loadedPreviewItems
                .OrderBy(previewItem => !previewItem.IsRelocatable)
                .ToList();
        }
        finally
        {
            isLoadingPreview = false;
        }
    }

    private async Task RelocateAsync()
    {
        var selectedBasePath = basePath!;
        isRelocating = true;

        try
        {
            var result = await Task.Run(() =>
                operationRunner.RunAsync(
                    (ArchiveFolderRelocationService service) =>
                        service.RelocateAsync(
                            archiveIds: ArchiveIds,
                            basePath: selectedBasePath,
                            cancellationToken: CancellationToken.None
                        )
                )
            );

            await DialogRef.CloseAsync(DialogResult.Ok(result));
        }
        finally
        {
            isRelocating = false;
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
