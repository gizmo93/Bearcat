using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
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

    private ReleaseTemplateDetailReadModel releaseTemplate = null!;
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
                ReleaseContentType = releaseTemplate.ReleaseContentType,
                ReleaseGroupId = releaseTemplate.ReleaseGroupId,
                UseReleaseCollections =
                    releaseTemplate.ReleaseCollectionDetectionMode
                        is ReleaseCollectionDetectionMode.SeriesEpisodePattern
                            or ReleaseCollectionDetectionMode.CustomRegex,
                ReleaseCollectionDetectionMode = releaseTemplate.ReleaseCollectionDetectionMode
                    is ReleaseCollectionDetectionMode.SeriesEpisodePattern
                        or ReleaseCollectionDetectionMode.CustomRegex
                    ? releaseTemplate.ReleaseCollectionDetectionMode
                    : ReleaseCollectionDetectionMode.SeriesEpisodePattern,
                ReleaseCollectionPattern = releaseTemplate.ReleaseCollectionPattern,
                ReleaseCollectionKeyTemplate = releaseTemplate.ReleaseCollectionKeyTemplate,
                ReleaseCollectionNameTemplate = releaseTemplate.ReleaseCollectionNameTemplate,
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

    private async Task ShowEditArchiveConfigDialogAsync(
        ArchiveConfigTemplateReadModel archiveConfig
    )
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

    private async Task DeleteArchiveConfigTemplateAsync(
        ArchiveConfigTemplateReadModel archiveConfig
    )
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
        if (
            releaseTemplate.ReleaseType is ReleaseType.Unmanaged
            && releaseTemplate.ArchiveConfigTemplates.Count == 0
        )
        {
            var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();
            await service.EnsureUnmanagedArchiveConfigTemplateAsync(ReleaseTemplateId);
            await LoadReleaseTemplateAsync();
        }

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

    private async Task ShowEditUploadConfigDialogAsync(UploadConfigTemplateReadModel uploadConfig)
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
                    PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
                    CollectionUploadSlotKey = uploadConfig.CollectionUploadSlotKey,
                    CollectionUploadSlotName = uploadConfig.CollectionUploadSlotName,
                    CollectionUploadSlotIsRequired = uploadConfig.CollectionUploadSlotIsRequired,
                    CollectionUploadSlotPasswordPolicy =
                        uploadConfig.CollectionUploadSlotPasswordPolicy,
                    CollectionUploadSlotExpectedArchivePassword =
                        uploadConfig.CollectionUploadSlotExpectedArchivePassword,
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

    private async Task DeleteUploadConfigTemplateAsync(UploadConfigTemplateReadModel uploadConfig)
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

    private async Task ShowAddImageUploadConfigDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditImageUploadConfigTemplateDialog.ReleaseTemplateId)] =
                ReleaseTemplateId,
            [nameof(CreateOrEditImageUploadConfigTemplateDialog.FormModel)] =
                new ImageUploadConfigTemplateFormModel(),
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditImageUploadConfigTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddImageUploadConfig"],
                Description = L["ImageUploadConfigTemplateDialogDescription"],
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

    private async Task ShowEditImageUploadConfigDialogAsync(
        ImageUploadConfigTemplateReadModel imageUploadConfig
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditImageUploadConfigTemplateDialog.ReleaseTemplateId)] =
                ReleaseTemplateId,
            [nameof(CreateOrEditImageUploadConfigTemplateDialog.ImageUploadConfigTemplateId)] =
                imageUploadConfig.ImageUploadConfigTemplateId,
            [nameof(CreateOrEditImageUploadConfigTemplateDialog.FormModel)] =
                new ImageUploadConfigTemplateFormModel
                {
                    Name = imageUploadConfig.Name,
                    ImageHosterRegistrationId = imageUploadConfig.ImageHosterRegistrationId,
                },
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditImageUploadConfigTemplateDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", imageUploadConfig.DisplayName],
                Description = L["ImageUploadConfigTemplateDialogDescription"],
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

    private async Task DeleteImageUploadConfigTemplateAsync(
        ImageUploadConfigTemplateReadModel imageUploadConfig
    )
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", imageUploadConfig.DisplayName],
            L["DeleteImageUploadConfigTemplateConfirmation", imageUploadConfig.DisplayName],
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
        await service.DeleteImageUploadConfigTemplateAsync(
            imageUploadConfig.ImageUploadConfigTemplateId
        );
        await LoadReleaseTemplateAsync();
    }

    private async Task ShowAddCollectionImageUploadConfigDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditCollectionImageUploadConfigTemplateDialog.ReleaseTemplateId)] =
                ReleaseTemplateId,
            [nameof(CreateOrEditCollectionImageUploadConfigTemplateDialog.FormModel)] =
                new ImageUploadConfigTemplateFormModel(),
        };

        var dialog =
            await dialogService.OpenAsync<CreateOrEditCollectionImageUploadConfigTemplateDialog>(
                parameters,
                new DialogOpenOptions
                {
                    Title = L["AddCollectionImageUploadConfig"],
                    Description = L["CollectionImageUploadConfigTemplateDialogDescription"],
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

    private async Task ShowEditCollectionImageUploadConfigDialogAsync(
        ImageUploadConfigTemplateReadModel imageUploadConfig
    )
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditCollectionImageUploadConfigTemplateDialog.ReleaseTemplateId)] =
                ReleaseTemplateId,
            [
                nameof(
                    CreateOrEditCollectionImageUploadConfigTemplateDialog.CollectionImageUploadConfigTemplateId
                )
            ] = imageUploadConfig.ImageUploadConfigTemplateId,
            [nameof(CreateOrEditCollectionImageUploadConfigTemplateDialog.FormModel)] =
                new ImageUploadConfigTemplateFormModel
                {
                    Name = imageUploadConfig.Name,
                    ImageHosterRegistrationId = imageUploadConfig.ImageHosterRegistrationId,
                },
        };

        var dialog =
            await dialogService.OpenAsync<CreateOrEditCollectionImageUploadConfigTemplateDialog>(
                parameters,
                new DialogOpenOptions
                {
                    Title = L["EditNamedItem", imageUploadConfig.DisplayName],
                    Description = L["CollectionImageUploadConfigTemplateDialogDescription"],
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

    private async Task DeleteCollectionImageUploadConfigTemplateAsync(
        ImageUploadConfigTemplateReadModel imageUploadConfig
    )
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", imageUploadConfig.DisplayName],
            L["DeleteImageUploadConfigTemplateConfirmation", imageUploadConfig.DisplayName],
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
        await service.DeleteCollectionImageUploadConfigTemplateAsync(
            imageUploadConfig.ImageUploadConfigTemplateId
        );
        await LoadReleaseTemplateAsync();
    }

    private async Task ShowAddLinkCrypterDialogAsync(UploadConfigTemplateReadModel uploadConfig)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditUploadConfigLinkCrypterTemplateDialog.UploadConfigTemplateId)] =
                uploadConfig.UploadConfigTemplateId,
            [nameof(CreateOrEditUploadConfigLinkCrypterTemplateDialog.FormModel)] =
                new UploadConfigLinkCrypterTemplateFormModel
                {
                    ContainerScope = string.IsNullOrWhiteSpace(uploadConfig.CollectionUploadSlotKey)
                        ? LinkCrypterContainerScope.Release
                        : LinkCrypterContainerScope.ReleaseCollection,
                },
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
        UploadConfigTemplateReadModel uploadConfig,
        UploadConfigLinkCrypterTemplateReadModel linkCrypter
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
                    ContainerScope = linkCrypter.ContainerScope,
                    Password = linkCrypter.Password,
                    EnableCaptcha = linkCrypter.EnableCaptcha,
                    EnableContainerDownload = linkCrypter.EnableContainerDownload,
                    EnableClickAndLoad = linkCrypter.EnableClickAndLoad,
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
        UploadConfigLinkCrypterTemplateReadModel linkCrypter
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
