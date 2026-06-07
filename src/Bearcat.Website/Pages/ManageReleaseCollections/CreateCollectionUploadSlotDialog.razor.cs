using System.Text;
using Bearcat.Domain.UseCases.ManageReleaseCollections;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public partial class CreateCollectionUploadSlotDialog : OwningComponentBase
{
    [Parameter]
    public CollectionUploadSlotFormModel FormModel { get; set; } = new();

    [Parameter]
    public IReadOnlyList<string> ExistingSlotKeys { get; set; } = [];

    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;

    private IEnumerable<SelectOption<CollectionUploadSlotPasswordPolicy>> PasswordPolicyOptions =>
        Enum.GetValues<CollectionUploadSlotPasswordPolicy>()
            .Select(policy => new SelectOption<CollectionUploadSlotPasswordPolicy>(
                policy,
                L.Localize(policy)
            ));

    protected override void OnInitialized()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += HandleValidationRequested;
    }

    private async Task SaveAsync()
    {
        var service = ScopedServices.GetRequiredService<ReleaseCollectionService>();
        var id = await service.CreateUploadSlotAsync(
            FormModel.ReleaseCollectionId,
            FormModel.Name,
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
