using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class CreateOrEditReleaseDialog(
    DialogService dialogService,
    IConfiguration configuration,
    NavigationManager navigationManager,
    IReleaseGroupReadRepository releaseGroupReadRepository
) : OwningComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public ReleaseFormModel? FormModel { get; set; }

    [Parameter]
    public int? ReleaseId { get; set; }

    private IReadOnlyList<ReleaseGroupReadModel> releaseGroups = [];
    private ReleaseFormModel formModel = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore? messageStore;
    private string? folderValidationMessage;

    private IEnumerable<SelectOption<int>> ReleaseGroupOptions =>
        releaseGroups.Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name));

    private string GetReleaseGroupDisplayText(int releaseGroupId)
    {
        return releaseGroups.FirstOrDefault(group => group.ReleaseGroupId == releaseGroupId)?.Name
            ?? releaseGroupId.ToString();
    }

    protected override async Task OnInitializedAsync()
    {
        formModel = FormModel ?? new ReleaseFormModel();
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        releaseGroups = await releaseGroupReadRepository.GetAllAsync();

        if (releaseGroups.Count > 0 && formModel.ReleaseGroupId == 0)
        {
            formModel.ReleaseGroupId = releaseGroups[0].ReleaseGroupId;
        }
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseService>();

        if (formModel.IsEdit && ReleaseId is not null)
        {
            await service.UpdateAsync(
                releaseId: ReleaseId.Value,
                name: formModel.Name,
                releaseFolderPath: formModel.FolderPath,
                releaseGroupId: formModel.ReleaseGroupId
            );

            await DialogRef.CloseAsync(DialogResult.Ok(ReleaseId.Value));
            return;
        }

        var id = await service.CreateAsync(
            name: formModel.Name,
            releaseFolderPath: formModel.FolderPath,
            releaseType: formModel.ReleaseType,
            releaseGroupId: formModel.ReleaseGroupId
        );

        await DialogRef.CloseAsync(DialogResult.Ok(id));
        navigationManager.NavigateTo("releases");
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        folderValidationMessage = null;
        messageStore!.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name, L["NameIsRequired"]);
        }

        if (string.IsNullOrWhiteSpace(formModel.FolderPath))
        {
            folderValidationMessage = L["SelectFolderRequired"];
            messageStore.Add(() => formModel.FolderPath, folderValidationMessage);
        }

        if (formModel.ReleaseGroupId == 0)
        {
            messageStore.Add(() => formModel.ReleaseGroupId, L["SelectReleaseGroupRequired"]);
        }
    }

    private async Task OpenFolderDialogAsync()
    {
        var releasesPath = configuration.GetRequiredSection("ReleaseDataDirectory").Value!;
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPath)] = releasesPath,
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = formModel.FolderPath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectReleaseFolder"],
                Description = L["SelectReleaseFolderDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var folderPath = result.GetData<string>();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        formModel.FolderPath = folderPath;
        folderValidationMessage = null;

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            formModel.Name = Path.GetFileName(folderPath);
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
