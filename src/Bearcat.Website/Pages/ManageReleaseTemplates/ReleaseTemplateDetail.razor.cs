using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Dto;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class ReleaseTemplateDetail(
    IReleaseTemplateReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService,
    NavigationManager navigationManager
) : OwningComponentBase
{
    [Parameter]
    public int ReleaseTemplateId { get; set; }

    private ReleaseTemplateDetailDto releaseTemplate = null!;
    private bool isInitialized;

    protected override async Task OnInitializedAsync()
    {
        await LoadReleaseTemplateAsync();
    }

    private async Task LoadReleaseTemplateAsync()
    {
        var detail = await readRepository.GetDetailAsync(ReleaseTemplateId);

        if (detail is null)
        {
            navigationManager.NotFound();
            return;
        }

        releaseTemplate = detail;
        isInitialized = true;
    }

    private async Task ShowEditTemplateDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditReleaseTemplateDialog.FormModel)] = new ReleaseTemplateFormModel
            {
                ReleaseTemplateId = releaseTemplate.ReleaseTemplateId,
                Name = releaseTemplate.Name,
                ReleaseType = releaseTemplate.ReleaseType,
                ReleaseGroupId = releaseTemplate.ReleaseGroupId,
                IsEdit = true,
            },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditReleaseTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", releaseTemplate.Name],
                Description = L["ReleaseTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task DeleteTemplateAsync()
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", releaseTemplate.Name],
            L["DeleteReleaseTemplateConfirmation", releaseTemplate.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
        await service.DeleteAsync(releaseTemplate.ReleaseTemplateId);
        navigationManager.NavigateTo("/release-templates");
    }

    private async Task ShowAddArchiveConfigDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditArchiveConfigTemplateDialog.ReleaseTemplateId)] = ReleaseTemplateId,
            [nameof(CreateOrEditArchiveConfigTemplateDialog.FormModel)] =
                new ArchiveConfigTemplateFormModel(),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditArchiveConfigTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddArchiveConfiguration"],
                Description = L["ArchiveConfigTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task ShowEditArchiveConfigDialogAsync(ArchiveConfigTemplateDto archiveConfig)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditArchiveConfigTemplateDialog.ReleaseTemplateId)] = ReleaseTemplateId,
            [nameof(CreateOrEditArchiveConfigTemplateDialog.ArchiveConfigTemplateId)] =
                archiveConfig.ArchiveConfigTemplateId,
            [nameof(CreateOrEditArchiveConfigTemplateDialog.FormModel)] =
                new ArchiveConfigTemplateFormModel
                {
                    Name = archiveConfig.Name,
                    ArchiverName = archiveConfig.ArchiverName,
                    ArchiveFilesBasePath = archiveConfig.ArchiveFilesBasePath,
                    ArchivePassword = archiveConfig.ArchivePassword,
                    ArchiveFileSizeMb = archiveConfig.ArchiveFileSizeMb,
                    UseReleaseNameAsArchiveName = archiveConfig.UseReleaseNameAsArchiveName,
                    IsEdit = true,
                },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditArchiveConfigTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", archiveConfig.Name],
                Description = L["ArchiveConfigTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task DeleteArchiveConfigTemplateAsync(ArchiveConfigTemplateDto archiveConfig)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", archiveConfig.Name],
            L["DeleteArchiveConfigTemplateConfirmation", archiveConfig.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
        await service.DeleteArchiveConfigTemplateAsync(archiveConfig.ArchiveConfigTemplateId);
        await LoadReleaseTemplateAsync();
    }

    private async Task ShowAddUploadConfigDialogAsync()
    {
        if (releaseTemplate.ArchiveConfigTemplates.Count == 0)
        {
            toastService.Error(L["CreateArchiveConfigTemplateFirst"]);
            return;
        }

        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigTemplateDialog.ReleaseTemplateId)] = ReleaseTemplateId,
            [nameof(CreateOrEditUploadConfigTemplateDialog.FormModel)] =
                new UploadConfigTemplateFormModel
                {
                    ArchiveConfigTemplateId = releaseTemplate
                        .ArchiveConfigTemplates.First()
                        .ArchiveConfigTemplateId,
                },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditUploadConfigTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddUploadConfig"],
                Description = L["UploadConfigTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task ShowEditUploadConfigDialogAsync(UploadConfigTemplateDto uploadConfig)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigTemplateDialog.ReleaseTemplateId)] = ReleaseTemplateId,
            [nameof(CreateOrEditUploadConfigTemplateDialog.UploadConfigTemplateId)] =
                uploadConfig.UploadConfigTemplateId,
            [nameof(CreateOrEditUploadConfigTemplateDialog.FormModel)] =
                new UploadConfigTemplateFormModel
                {
                    Name = uploadConfig.Name,
                    HosterRegistrationId = uploadConfig.HosterRegistrationId,
                    ArchiveConfigTemplateId = uploadConfig.ArchiveConfigTemplateId,
                    LinksDistributedTo = uploadConfig.LinksDistributedTo.ToList(),
                    IsEdit = true,
                },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditUploadConfigTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", uploadConfig.DisplayName],
                Description = L["UploadConfigTemplateDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task DeleteUploadConfigTemplateAsync(UploadConfigTemplateDto uploadConfig)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", uploadConfig.DisplayName],
            L["DeleteUploadConfigTemplateConfirmation", uploadConfig.DisplayName],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
        await service.DeleteUploadConfigTemplateAsync(uploadConfig.UploadConfigTemplateId);
        await LoadReleaseTemplateAsync();
    }

    private async Task ShowAddLinkCrypterDialogAsync(UploadConfigTemplateDto uploadConfig)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigLinkCrypterTemplateDialog.UploadConfigTemplateId)] =
                uploadConfig.UploadConfigTemplateId,
            [nameof(CreateOrEditUploadConfigLinkCrypterTemplateDialog.FormModel)] =
                new UploadConfigLinkCrypterTemplateFormModel(),
        };

        var dialog =
            await dialogService.OpenAsync<CreateOrEditUploadConfigLinkCrypterTemplateDialog>(
                parameters,
                new DialogOpenOptions
                {
                    Title = L["AddLinkCrypter"],
                    Description = L["LinkCrypterTemplateDialogDescription"],
                    Size = DialogSize.Large,
                    ShowClose = true,
                    PreventClose = true,
                }
            );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task ShowEditLinkCrypterDialogAsync(
        UploadConfigTemplateDto uploadConfig,
        UploadConfigLinkCrypterTemplateDto linkCrypter
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigLinkCrypterTemplateDialog.UploadConfigTemplateId)] =
                uploadConfig.UploadConfigTemplateId,
            [
                nameof(
                    CreateOrEditUploadConfigLinkCrypterTemplateDialog.UploadConfigLinkCrypterTemplateId
                )
            ] = linkCrypter.UploadConfigLinkCrypterTemplateId,
            [nameof(CreateOrEditUploadConfigLinkCrypterTemplateDialog.FormModel)] =
                new UploadConfigLinkCrypterTemplateFormModel
                {
                    LinkCrypterRegistrationId = linkCrypter.LinkCrypterRegistrationId,
                    Password = linkCrypter.Password,
                    IsEdit = true,
                },
        };

        var dialog =
            await dialogService.OpenAsync<CreateOrEditUploadConfigLinkCrypterTemplateDialog>(
                parameters,
                new DialogOpenOptions
                {
                    Title = L["EditNamedItem", linkCrypter.LinkCrypterRegistrationName],
                    Description = L["LinkCrypterTemplateDialogDescription"],
                    Size = DialogSize.Large,
                    ShowClose = true,
                    PreventClose = true,
                }
            );

        if (!dialog.Cancelled)
        {
            await LoadReleaseTemplateAsync();
        }
    }

    private async Task DeleteLinkCrypterTemplateAsync(
        UploadConfigLinkCrypterTemplateDto linkCrypter
    )
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", linkCrypter.LinkCrypterRegistrationName],
            L["DeleteLinkCrypterTemplateConfirmation", linkCrypter.LinkCrypterRegistrationName],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
        await service.DeleteUploadConfigLinkCrypterTemplateAsync(
            linkCrypter.UploadConfigLinkCrypterTemplateId
        );
        await LoadReleaseTemplateAsync();
    }
}
