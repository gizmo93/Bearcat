using System.Text;
using Bearcat.Domain.UseCases.ManageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageHosters.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class CreateCollectionUploadSlotDialog(
    IHosterConfigurationReadRepository hosterReadRepository,
    IReleaseCollectionReadRepository releaseCollectionReadRepository
) : OwningComponentBase
{
    [Parameter]
    public CollectionUploadSlotFormModel FormModel { get; set; } = new();

    [Parameter]
    public IReadOnlyList<string> ExistingSlotKeys { get; set; } = [];

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private IReadOnlyList<HosterRegistrationReadModel> hosterRegistrations = [];
    private IReadOnlyList<CollectionArchiveConfigOptionReadModel> archiveConfigOptions = [];
    private bool isInitialized;

    private IEnumerable<SelectOption<CollectionUploadSlotPasswordPolicy>> PasswordPolicyOptions =>
        Enum.GetValues<CollectionUploadSlotPasswordPolicy>()
            .Select(policy => new SelectOption<CollectionUploadSlotPasswordPolicy>(
                policy,
                L.Localize(policy)
            ));

    private IEnumerable<SelectOption<int?>> HosterRegistrationOptions =>
        hosterRegistrations
            .Where(hoster => hoster.IsActive || hoster.Id == FormModel.HosterRegistrationId)
            .OrderBy(hoster => hoster.Name)
            .Select(hoster => new SelectOption<int?>(hoster.Id, hoster.Name));

    private IEnumerable<SelectOption<string>> ArchiveConfigOptions =>
        archiveConfigOptions.Select(config => new SelectOption<string>(config.Name, config.Name));

    private HosterRegistrationReadModel? SelectedHosterRegistration =>
        FormModel.HosterRegistrationId is null
            ? null
            : hosterRegistrations.FirstOrDefault(hoster =>
                hoster.Id == FormModel.HosterRegistrationId
            );

    private bool CanUsePremiumOnlyDownload =>
        SelectedHosterRegistration?.SupportsPremiumOnlyDownloads is true;

    protected override async Task OnInitializedAsync()
    {
        hosterRegistrations = await hosterReadRepository.GetAllRegistrationsAsync();
        archiveConfigOptions = await releaseCollectionReadRepository.GetArchiveConfigOptionsAsync(
            FormModel.ReleaseCollectionId
        );

        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        var id = await service.CreateUploadSlotAsync(
            FormModel.ReleaseCollectionId,
            FormModel.Name,
            FormModel.HosterRegistrationId!.Value,
            FormModel.ArchiveConfigName!,
            CanUsePremiumOnlyDownload && FormModel.PremiumOnlyDownload,
            FormModel.IsRequired,
            FormModel.PasswordPolicy,
            FormModel.ExpectedArchivePassword
        );

        await DialogRef.CloseAsync(DialogResult.Ok(id));
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        messageStore.Clear();

        if (string.IsNullOrWhiteSpace(FormModel.Name))
        {
            messageStore.Add(() => FormModel.Name, L["NameIsRequired"]);
            return;
        }

        if (FormModel.HosterRegistrationId is null)
        {
            messageStore.Add(
                () => FormModel.HosterRegistrationId!,
                L["HosterRegistrationRequired"]
            );
        }

        if (string.IsNullOrWhiteSpace(FormModel.ArchiveConfigName))
        {
            messageStore.Add(() => FormModel.ArchiveConfigName!, L["ArchiveConfigRequired"]);
        }

        var key = CreateStableKey(FormModel.Name);
        if (string.IsNullOrWhiteSpace(key))
        {
            messageStore.Add(() => FormModel.Name, L["CollectionUploadSlotKeyCouldNotBeDerived"]);
        }

        if (ExistingSlotKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            messageStore.Add(() => FormModel.Name, L["CollectionUploadSlotKeyAlreadyExists"]);
        }

        if (
            FormModel.PasswordPolicy is CollectionUploadSlotPasswordPolicy.MustEqualExpectedValue
            && string.IsNullOrWhiteSpace(FormModel.ExpectedArchivePassword)
        )
        {
            messageStore.Add(
                () => FormModel.ExpectedArchivePassword!,
                L["CollectionUploadSlotExpectedArchivePasswordRequired"]
            );
        }
    }

    private void OnHosterRegistrationChanged()
    {
        if (!CanUsePremiumOnlyDownload)
        {
            FormModel.PremiumOnlyDownload = false;
        }
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private static string CreateStableKey(string value)
    {
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
