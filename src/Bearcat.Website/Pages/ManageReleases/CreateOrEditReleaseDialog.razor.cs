using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleaseGroups.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseGroups.Repositories;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class CreateOrEditReleaseDialog(
    DialogService dialogService,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig,
    NavigationManager navigationManager,
    IScopedOperationRunner operationRunner
) : ComponentBase
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

    private bool ShowFolderSelection =>
        !(formModel.IsEdit && formModel.ReleaseType is ReleaseType.Unmanaged);

    private string FolderLabel =>
        formModel.ReleaseType is ReleaseType.Unmanaged ? L["ArchiveFolder"] : L["ReleaseFolder"];

    private IReadOnlyList<SelectOption<int>> ReleaseGroupOptions =>
        releaseGroups
            .Select(group => new SelectOption<int>(group.ReleaseGroupId, group.Name))
            .ToList();

    private IReadOnlyList<SelectOption<ReleaseType>> ReleaseTypeOptions =>
        Enum.GetValues<ReleaseType>()
            .Select(type => new SelectOption<ReleaseType>(type, L.Localize(type)))
            .ToList();

    private IReadOnlyList<SelectOption<ReleaseContentType>> ReleaseContentTypeOptions =>
        Enum.GetValues<ReleaseContentType>()
            .Select(type => new SelectOption<ReleaseContentType>(type, L.Localize(type)))
            .ToList();

    private IReadOnlyList<SelectOption<string>> LanguageOptions =>
        [
            new(string.Empty, L["Unknown"]),
            .. CultureInfo
                .GetCultures(CultureTypes.NeutralCultures)
                .Where(culture => culture.TwoLetterISOLanguageName.Length == 2)
                .DistinctBy(culture => culture.TwoLetterISOLanguageName)
                .OrderBy(culture => culture.NativeName)
                .Select(culture => new SelectOption<string>(
                    culture.TwoLetterISOLanguageName,
                    culture.NativeName
                )),
        ];

    private string GetReleaseGroupDisplayText(int releaseGroupId)
    {
        return releaseGroups.FirstOrDefault(group => group.ReleaseGroupId == releaseGroupId)?.Name
            ?? releaseGroupId.ToString();
    }

    private string GetReleaseTypeDisplayText(ReleaseType releaseType)
    {
        return L.Localize(releaseType);
    }

    private string GetReleaseContentTypeDisplayText(ReleaseContentType releaseContentType)
    {
        return L.Localize(releaseContentType);
    }

    protected override async Task OnInitializedAsync()
    {
        formModel = FormModel ?? new ReleaseFormModel();
        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;

        releaseGroups = await operationRunner.RunAsync(
            (IReleaseGroupReadRepository repository) => repository.GetAllAsync()
        );

        if (releaseGroups.Count > 0 && formModel.ReleaseGroupId == 0)
        {
            formModel.ReleaseGroupId = releaseGroups[0].ReleaseGroupId;
        }
    }

    private async Task SaveAsync()
    {
        if (formModel.IsEdit && ReleaseId is not null)
        {
            await operationRunner.RunAsync(
                (ReleaseService service) =>
                    service.UpdateAsync(
                        releaseId: ReleaseId.Value,
                        name: formModel.Name,
                        releaseFolderPath: formModel.FolderPath,
                        releaseContentType: formModel.ReleaseContentType,
                        releaseGroupId: formModel.ReleaseGroupId,
                        primaryLanguageCode: formModel.PrimaryLanguageCode
                    )
            );

            await DialogRef.CloseAsync(DialogResult.Ok(ReleaseId.Value));
            return;
        }

        var id = await operationRunner.RunAsync(
            (ReleaseService service) =>
                service.CreateAsync(
                    name: formModel.Name,
                    releaseFolderPath: formModel.FolderPath,
                    releaseType: formModel.ReleaseType,
                    releaseContentType: formModel.ReleaseContentType,
                    releaseGroupId: formModel.ReleaseGroupId,
                    primaryLanguageCode: formModel.PrimaryLanguageCode
                )
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

        if (ShowFolderSelection && string.IsNullOrWhiteSpace(formModel.FolderPath))
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
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = formModel.FolderPath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title =
                    formModel.ReleaseType is ReleaseType.Unmanaged
                        ? L["SelectExistingArchiveFolder"]
                        : L["SelectReleaseFolder"],
                Description =
                    formModel.ReleaseType is ReleaseType.Unmanaged
                        ? L["SelectExistingArchiveFolderDescription"]
                        : L["SelectReleaseFolderDescription"],
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
