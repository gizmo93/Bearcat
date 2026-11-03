using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;
using Bearcat.Domain.UseCases.ManageLinkCrypters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Website.Pages.ManageReleases;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class NewLinkCrypterImportDialog(
    DialogService dialogService,
    IScopedOperationRunner operationRunner,
    IOptions<WorkingDirectoriesConfig> workingDirectoriesConfig
) : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<LinkCrypterRegistrationReadModel> linkCrypterRegistrations = [];
    private IReadOnlyList<ReleaseTemplateSummaryReadModel> releaseTemplates = [];
    private int? linkCrypterRegistrationId;
    private int? releaseTemplateId;
    private string? archiveFilesBasePath;
    private string primaryLanguageCode = string.Empty;
    private string? errorMessage;
    private bool isLoaded;
    private bool isStarting;

    private IReadOnlyList<SelectOption<int?>> LinkCrypterRegistrationOptions =>
        linkCrypterRegistrations
            .Select(registration => new SelectOption<int?>(
                registration.LinkCrypterRegistrationId,
                $"{registration.Name} ({registration.CrypterName})"
            ))
            .ToList();

    private IReadOnlyList<SelectOption<int?>> ReleaseTemplateOptions =>
        releaseTemplates
            .Select(template => new SelectOption<int?>(template.ReleaseTemplateId, template.Name))
            .ToList();

    private IReadOnlyList<SelectOption<string>> LanguageOptions =>
        PrimaryLanguageSelectOptions.Create(L["Unknown"]);

    protected override async Task OnInitializedAsync()
    {
        var allLinkCrypterRegistrations = await operationRunner.RunAsync(
            (ILinkCrypterRegistrationReadRepository repository) => repository.GetAllAsync()
        );

        linkCrypterRegistrations = operationRunner.Run(
            (LinkCrypterImportService service) =>
                allLinkCrypterRegistrations
                    .Where(registration =>
                        registration.IsActive
                        && service.CanReadContainers(registration.LinkCrypterClassName)
                    )
                    .ToList()
        );

        releaseTemplates = await operationRunner.RunAsync(
            (IReleaseTemplateReadRepository repository) => repository.GetAllAsync()
        );

        isLoaded = true;
    }

    private async Task OpenFolderDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(FolderSelectionDialog.BaseFolderPaths)] =
                workingDirectoriesConfig.Value.GetWorkingDirectories(),
            [nameof(FolderSelectionDialog.SelectedFolderPath)] = archiveFilesBasePath,
        };

        var result = await dialogService.OpenAsync<FolderSelectionDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["SelectArchiveFolder"],
                Description = L["SelectArchiveFolderDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
            }
        );

        if (result.Cancelled)
        {
            return;
        }

        var selectedPath = result.GetData<string>();

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            archiveFilesBasePath = selectedPath;
        }
    }

    private async Task StartAsync()
    {
        isStarting = true;
        errorMessage = null;

        try
        {
            var linkCrypterImportId = await operationRunner.RunAsync(
                (LinkCrypterImportService service) =>
                    service.StartAsync(
                        linkCrypterRegistrationId: linkCrypterRegistrationId!.Value,
                        releaseTemplateId: releaseTemplateId!.Value,
                        archiveFilesBasePathForReleasesWithoutLocalFiles: archiveFilesBasePath,
                        primaryLanguageCode: primaryLanguageCode
                    )
            );

            await DialogRef.CloseAsync(DialogResult.Ok(linkCrypterImportId));
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isStarting = false;
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
