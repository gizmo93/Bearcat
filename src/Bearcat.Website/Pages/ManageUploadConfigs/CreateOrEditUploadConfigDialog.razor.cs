using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageUploadConfigs;
using Bearcat.Domain.UseCases.ManageUploadConfigs.ReadModels;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageUploadConfigs;

public partial class CreateOrEditUploadConfigDialog : OwningComponentBase
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int? UploadConfigId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private bool IsEdit => UploadConfigId.HasValue;
    private IUploadConfigReadRepository readRepository = null!;
    private UploadConfigFormModel formModel = null!;
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<ArchiveConfigOptionReadModel> archiveConfigOptions = [];
    private bool isInitialized;

    private IEnumerable<SelectOption<int?>> HosterRegistrationOptions =>
        hosterRegistrations
            .Where(hoster => hoster.IsActive || hoster.Id == formModel.HosterRegistrationId)
            .OrderBy(hoster => hoster.Name)
            .Select(hoster => new SelectOption<int?>(hoster.Id, hoster.Name));

    private HosterRegistrationReadModel? SelectedHosterRegistration =>
        formModel.HosterRegistrationId is null
            ? null
            : hosterRegistrations.FirstOrDefault(hoster =>
                hoster.Id == formModel.HosterRegistrationId
            );

    private bool CanUsePremiumOnlyDownload =>
        SelectedHosterRegistration?.SupportsPremiumOnlyDownloads is true;

    private ArchiveConfigOptionReadModel? SelectedArchiveConfig =>
        formModel.ArchiveConfigId is null
            ? null
            : archiveConfigOptions.FirstOrDefault(config =>
                config.ArchiveConfigId == formModel.ArchiveConfigId
            );

    protected override async Task OnInitializedAsync()
    {
        readRepository = ScopedServices.GetRequiredService<IUploadConfigReadRepository>();
        var hosterReadRepository =
            ScopedServices.GetRequiredService<IHosterConfigurationReadRepository>();

        await InitializeFormModelAsync();
        hosterRegistrations = await hosterReadRepository.GetAllRegistrationsAsync();
        archiveConfigOptions = await readRepository.GetArchiveConfigOptionsAsync(ReleaseId);
        ResetPremiumOnlyDownloadIfUnsupported();

        editContext = new EditContext(formModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<UploadConfigService>();

        if (IsEdit)
        {
            await service.UpdateAsync(
                uploadConfigId: UploadConfigId!.Value,
                name: formModel.Name!,
                hosterRegistrationId: formModel.HosterRegistrationId!.Value,
                archiveConfigId: formModel.ArchiveConfigId!.Value,
                premiumOnlyDownload: CanUsePremiumOnlyDownload && formModel.PremiumOnlyDownload
            );
        }
        else
        {
            await service.CreateAsync(
                releaseId: ReleaseId,
                name: formModel.Name!,
                hosterRegistrationId: formModel.HosterRegistrationId!.Value,
                archiveConfigId: formModel.ArchiveConfigId!.Value,
                premiumOnlyDownload: CanUsePremiumOnlyDownload && formModel.PremiumOnlyDownload
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(formModel.Name))
        {
            messageStore.Add(() => formModel.Name!, L["NameIsRequired"]);
        }

        if (formModel.HosterRegistrationId is null)
        {
            messageStore.Add(
                () => formModel.HosterRegistrationId!,
                L["HosterRegistrationRequired"]
            );
        }

        if (formModel.ArchiveConfigId is null)
        {
            messageStore.Add(() => formModel.ArchiveConfigId!, L["ArchiveConfigRequired"]);
        }

        if (
            SelectedHosterRegistration?.MaxFileSizeMb is { } maxFileSizeMb
            && SelectedArchiveConfig is { } archiveConfig
            && archiveConfig.ArchiveFileSizeMb > maxFileSizeMb
        )
        {
            messageStore.Add(
                () => formModel.ArchiveConfigId!,
                L[
                    "ArchiveFileSizeExceedsHosterLimit",
                    archiveConfig.ArchiveFileSizeMb,
                    maxFileSizeMb
                ]
            );
        }
    }

    private void OnHosterRegistrationChanged()
    {
        ResetPremiumOnlyDownloadIfUnsupported();
    }

    private void ResetPremiumOnlyDownloadIfUnsupported()
    {
        if (!CanUsePremiumOnlyDownload)
        {
            formModel.PremiumOnlyDownload = false;
        }
    }

    private async Task InitializeFormModelAsync()
    {
        if (!IsEdit)
        {
            formModel = new UploadConfigFormModel();
            return;
        }

        var uploadConfig = await readRepository.GetReadModelByIdAsync(UploadConfigId!.Value);

        formModel = new UploadConfigFormModel
        {
            Name = uploadConfig.Name,
            HosterRegistrationId = uploadConfig.HosterRegistrationId,
            ArchiveConfigId = uploadConfig.ArchiveConfigId,
            PremiumOnlyDownload = uploadConfig.PremiumOnlyDownload,
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
