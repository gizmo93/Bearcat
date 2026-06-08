using System.Text;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseTemplates;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseTemplates.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public partial class CreateOrEditUploadConfigTemplateDialog(
    IHosterConfigurationReadRepository hosterReadRepository,
    IReleaseTemplateReadRepository releaseTemplateReadRepository
) : OwningComponentBase
{
    [Parameter]
    public UploadConfigTemplateFormModel FormModel { get; set; } = null!;

    [Parameter]
    public int ReleaseTemplateId { get; set; }

    [Parameter]
    public int? UploadConfigTemplateId { get; set; }

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<ArchiveConfigTemplateReadModel> archiveConfigTemplates = [];
    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isInitialized;
    private bool isEdit;
    private bool isUnmanagedReleaseTemplate;
    private bool usesReleaseCollections;

    private IEnumerable<SelectOption<int?>> HosterOptions =>
        hosterRegistrations
            .Where(hoster => hoster.IsActive || hoster.Id == FormModel.HosterRegistrationId)
            .OrderBy(hoster => hoster.Name)
            .Select(hoster => new SelectOption<int?>(hoster.Id, hoster.Name));

    private IEnumerable<SelectOption<int?>> ArchiveConfigOptions =>
        archiveConfigTemplates
            .OrderBy(config => config.Name)
            .Select(config => new SelectOption<int?>(config.ArchiveConfigTemplateId, config.Name));

    private IEnumerable<SelectOption<CollectionUploadSlotPasswordPolicy>> PasswordPolicyOptions =>
        Enum.GetValues<CollectionUploadSlotPasswordPolicy>()
            .Select(policy => new SelectOption<CollectionUploadSlotPasswordPolicy>(
                policy,
                L.Localize(policy)
            ));

    private HosterRegistrationReadModel? SelectedHosterRegistration =>
        FormModel.HosterRegistrationId is null
            ? null
            : hosterRegistrations.FirstOrDefault(hoster =>
                hoster.Id == FormModel.HosterRegistrationId
            );

    private bool CanUsePremiumOnlyDownload =>
        SelectedHosterRegistration?.SupportsPremiumOnlyDownloads is true;

    private bool useCollectionUploadSlot;

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isEdit = UploadConfigTemplateId is not null;

        hosterRegistrations = await hosterReadRepository.GetAllRegistrationsAsync();
        var detail = await releaseTemplateReadRepository.GetDetailAsync(ReleaseTemplateId);
        archiveConfigTemplates = detail?.ArchiveConfigTemplates ?? [];
        isUnmanagedReleaseTemplate = detail?.ReleaseType is ReleaseType.Unmanaged;
        usesReleaseCollections =
            detail?.ReleaseCollectionDetectionMode is not ReleaseCollectionDetectionMode.Disabled;

        useCollectionUploadSlot =
            usesReleaseCollections
            || !string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotKey)
            || !string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotName);

        if (
            string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotName)
            && !string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotKey)
        )
        {
            FormModel.CollectionUploadSlotName = FormModel.CollectionUploadSlotKey;
        }

        if (isUnmanagedReleaseTemplate && FormModel.ArchiveConfigTemplateId is null)
        {
            FormModel.ArchiveConfigTemplateId = archiveConfigTemplates
                .SingleOrDefault()
                ?.ArchiveConfigTemplateId;
        }

        ResetPremiumOnlyDownloadIfUnsupported();

        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        PrepareCollectionUploadSlotForSave();

        var service = ScopedServices.GetRequiredService<ReleaseTemplateService>();

        if (isEdit)
        {
            await service.UpdateUploadConfigTemplateAsync(
                UploadConfigTemplateId!.Value,
                FormModel.Name,
                FormModel.HosterRegistrationId!.Value,
                FormModel.ArchiveConfigTemplateId!.Value,
                CanUsePremiumOnlyDownload && FormModel.PremiumOnlyDownload,
                FormModel.LinksDistributedTo,
                FormModel.CollectionUploadSlotKey,
                FormModel.CollectionUploadSlotName,
                FormModel.CollectionUploadSlotIsRequired,
                FormModel.CollectionUploadSlotPasswordPolicy,
                FormModel.CollectionUploadSlotExpectedArchivePassword
            );
        }
        else
        {
            await service.CreateUploadConfigTemplateAsync(
                ReleaseTemplateId,
                FormModel.Name,
                FormModel.HosterRegistrationId!.Value,
                FormModel.ArchiveConfigTemplateId!.Value,
                CanUsePremiumOnlyDownload && FormModel.PremiumOnlyDownload,
                FormModel.LinksDistributedTo,
                FormModel.CollectionUploadSlotKey,
                FormModel.CollectionUploadSlotName,
                FormModel.CollectionUploadSlotIsRequired,
                FormModel.CollectionUploadSlotPasswordPolicy,
                FormModel.CollectionUploadSlotExpectedArchivePassword
            );
        }

        await DialogRef.CloseAsync(DialogResult.Ok());
    }

    private void AddLinkDistributedTo()
    {
        FormModel.LinksDistributedTo.Add(string.Empty);
    }

    private void DeleteLinkDistributedTo(int index)
    {
        FormModel.LinksDistributedTo.RemoveAt(index);
    }

    private void OnHosterRegistrationChanged()
    {
        ResetPremiumOnlyDownloadIfUnsupported();

        if (usesReleaseCollections && string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotName))
        {
            FormModel.CollectionUploadSlotName = GetDefaultCollectionUploadGroupName();
        }
    }

    private void ResetPremiumOnlyDownloadIfUnsupported()
    {
        if (!CanUsePremiumOnlyDownload)
        {
            FormModel.PremiumOnlyDownload = false;
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (FormModel.HosterRegistrationId is null)
        {
            messageStore.Add(
                () => FormModel.HosterRegistrationId!,
                L["SelectHosterRegistrationRequired"]
            );
        }

        if (!isUnmanagedReleaseTemplate && FormModel.ArchiveConfigTemplateId is null)
        {
            messageStore.Add(
                () => FormModel.ArchiveConfigTemplateId!,
                L["SelectArchiveConfigurationRequired"]
            );
        }

        if (
            usesReleaseCollections
            && useCollectionUploadSlot
            && string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotName)
        )
        {
            messageStore.Add(
                () => FormModel.CollectionUploadSlotName!,
                L["CollectionUploadGroupNameRequired"]
            );
        }

        if (
            useCollectionUploadSlot
            && FormModel.CollectionUploadSlotPasswordPolicy
                is CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
            && string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotExpectedArchivePassword)
        )
        {
            messageStore.Add(
                () => FormModel.CollectionUploadSlotExpectedArchivePassword!,
                L["CollectionUploadSlotExpectedArchivePasswordRequired"]
            );
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private void PrepareCollectionUploadSlotForSave()
    {
        if (!usesReleaseCollections || !useCollectionUploadSlot)
        {
            ClearCollectionUploadSlot();
            return;
        }

        FormModel.CollectionUploadSlotName = FormModel.CollectionUploadSlotName?.Trim();

        if (string.IsNullOrWhiteSpace(FormModel.CollectionUploadSlotKey))
        {
            FormModel.CollectionUploadSlotKey = CreateStableKey(FormModel.CollectionUploadSlotName);
        }

        if (
            FormModel.CollectionUploadSlotPasswordPolicy
            is not CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
        )
        {
            FormModel.CollectionUploadSlotExpectedArchivePassword = null;
        }
    }

    private void ClearCollectionUploadSlot()
    {
        FormModel.CollectionUploadSlotKey = null;
        FormModel.CollectionUploadSlotName = null;
        FormModel.CollectionUploadSlotIsRequired = false;
        FormModel.CollectionUploadSlotPasswordPolicy = CollectionUploadSlotPasswordPolicy.Ignore;
        FormModel.CollectionUploadSlotExpectedArchivePassword = null;
    }

    private string GetDefaultCollectionUploadGroupName()
    {
        if (!string.IsNullOrWhiteSpace(FormModel.Name))
        {
            return FormModel.Name.Trim();
        }

        return SelectedHosterRegistration?.Name ?? string.Empty;
    }

    private static string CreateStableKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var keyBuilder = new StringBuilder(value.Length);
        var lastWasSeparator = true;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                keyBuilder.Append(character);
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                keyBuilder.Append('-');
                lastWasSeparator = true;
            }
        }

        return keyBuilder.ToString().Trim('-');
    }
}
