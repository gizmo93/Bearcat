using BearCat.Core.Domain.UseCases.ManageArchives.Dto;
using BearCat.Core.Domain.UseCases.ManageArchives.Repositories;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Bearcat.Website.Pages.ManageArchives;

public partial class ArchiveDetailDialog(
    IArchiveReadRepository archiveReadRepository,
    ISnackbar snackbar)
    : ComponentBase
{
    [Parameter]
    public int ArchiveId { get; set; }

    [CascadingParameter]
    public IMudDialogInstance MudDialog { get; set; } = null!;

    private ArchiveDto Archive { get; set; } = null!;

    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        var archive = await archiveReadRepository.GetByIdAsync(ArchiveId);

        if (archive is null)
        {
            snackbar.Add($"Archive with ID {ArchiveId} not found.", Severity.Error);
            MudDialog.Cancel();
        }

        Archive = archive!;
        isInitialized = true;
    }
}

