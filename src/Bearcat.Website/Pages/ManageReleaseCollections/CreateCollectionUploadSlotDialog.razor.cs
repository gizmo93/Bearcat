using System.Text;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Domain.UseCases.ManageReleaseCollections.Repositories;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared.UploadTargets;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class CreateCollectionUploadSlotDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public CollectionUploadSlotFormModel FormModel { get; set; } = new();

    [Parameter]
    public IReadOnlyList<string> ExistingSlotKeys { get; set; } = [];

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private UploadTargetChoices uploadTargetChoices = null!;
    private IReadOnlyList<CollectionArchiveConfigOptionReadModel> archiveConfigOptions = [];
    private bool isInitialized;

    private IReadOnlyList<SelectOption<CollectionUploadSlotPasswordPolicy>> PasswordPolicyOptions =>
        Enum.GetValues<CollectionUploadSlotPasswordPolicy>()
            .Select(policy => new SelectOption<CollectionUploadSlotPasswordPolicy>(
                policy,
                L.Localize(policy)
            ))
            .ToList();

    private IReadOnlyList<SelectOption<string>> ArchiveConfigOptions =>
        archiveConfigOptions
            .Select(config => new SelectOption<string>(config.Name, config.Name))
            .ToList();

    private bool CanUsePremiumOnlyDownload =>
        uploadTargetChoices.SupportsPremiumOnlyDownloads(FormModel.UploadTarget);

    private CollectionArchiveConfigOptionReadModel? SelectedArchiveConfig =>
        string.IsNullOrWhiteSpace(FormModel.ArchiveConfigName)
            ? null
            : archiveConfigOptions.FirstOrDefault(config =>
                config.Name == FormModel.ArchiveConfigName
            );

    protected override async Task OnInitializedAsync()
    {
        uploadTargetChoices = await UploadTargetChoices.LoadAsync(operationRunner);
        archiveConfigOptions = await operationRunner.RunAsync(
            (IReleaseCollectionReadRepository repository) =>
                repository.GetArchiveConfigOptionsAsync(FormModel.ReleaseCollectionId)
        );

        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
        isInitialized = true;
    }

    private async Task SaveAsync()
    {
        var id = await operationRunner.RunAsync(
            (ReleaseCollectionService service) =>
                service.CreateUploadSlotAsync(
                    FormModel.ReleaseCollectionId,
                    FormModel.Name,
                    FormModel.UploadTarget!,
                    FormModel.ArchiveConfigName!,
                    CanUsePremiumOnlyDownload && FormModel.PremiumOnlyDownload,
                    FormModel.IsRequired,
                    FormModel.PasswordPolicy,
                    FormModel.ExpectedArchivePassword
                )
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

        if (FormModel.UploadTarget is null)
        {
            messageStore.Add(() => FormModel.UploadTarget!, L["UploadTargetRequired"]);
        }

        if (string.IsNullOrWhiteSpace(FormModel.ArchiveConfigName))
        {
            messageStore.Add(() => FormModel.ArchiveConfigName!, L["ArchiveConfigRequired"]);
        }

        if (
            uploadTargetChoices.GetMaxFileSizeMb(FormModel.UploadTarget) is { } maxFileSizeMb
            && SelectedArchiveConfig is { } archiveConfig
            && archiveConfig.ArchiveFileSizeMb > maxFileSizeMb
        )
        {
            messageStore.Add(
                () => FormModel.ArchiveConfigName!,
                L[
                    "ArchiveFileSizeExceedsHosterLimit",
                    archiveConfig.ArchiveFileSizeMb,
                    maxFileSizeMb
                ]
            );
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

    private void OnUploadTargetChanged()
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
