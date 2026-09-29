using Bearcat.Domain.UseCases.ManageProxyServers;
using Bearcat.Domain.UseCases.ManageProxyServers.Validation;
using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageProxyServers;

public partial class CreateOrEditProxyServerDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public ProxyServerFormModel FormModel { get; set; } = new();

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private bool isSaving;
    private bool isPasswordDisplayed;

    private IReadOnlyList<SelectOption<ProxyType>> ProxyTypeOptions =>
        Enum.GetValues<ProxyType>()
            .Select(proxyType => new SelectOption<ProxyType>(proxyType, L.Localize(proxyType)))
            .ToList();

    protected override void OnInitialized()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += (_, _) => messageStore.Clear();
        editContext.OnFieldChanged += (_, args) => ClearValidationMessages(args.FieldIdentifier);
    }

    private void ClearValidationMessages(FieldIdentifier field)
    {
        if (!editContext.GetValidationMessages(field).Any())
        {
            return;
        }

        messageStore.Clear(field);
        editContext.NotifyValidationStateChanged();
    }

    private void TogglePasswordVisibility()
    {
        isPasswordDisplayed = !isPasswordDisplayed;
    }

    private void ClearPasswordWhenRemovingStoredPassword()
    {
        if (FormModel.RemoveStoredPassword)
        {
            FormModel.Password = null;
        }
    }

    private async Task SaveAsync()
    {
        isSaving = true;

        var input = FormModel.ToInput();

        ProxyServerSaveResult result;

        try
        {
            result = FormModel.ProxyServerId is { } proxyServerId
                ? await operationRunner.RunAsync(
                    (ProxyServerService service) => service.UpdateAsync(proxyServerId, input)
                )
                : await operationRunner.RunAsync(
                    (ProxyServerService service) => service.CreateAsync(input)
                );
        }
        finally
        {
            isSaving = false;
        }

        if (!result.IsSuccess)
        {
            ShowValidationErrors(result.ValidationErrors);
            return;
        }

        await DialogRef.CloseAsync(DialogResult.Ok(result.ProxyServerId));
    }

    private void ShowValidationErrors(IReadOnlyList<ProxyServerValidationError> validationErrors)
    {
        messageStore.Clear();

        foreach (var validationError in validationErrors)
        {
            messageStore.Add(
                editContext.Field(GetFieldName(validationError)),
                L[$"ProxyServerValidationError.{validationError}"]
            );
        }

        editContext.NotifyValidationStateChanged();
    }

    private static string GetFieldName(ProxyServerValidationError validationError)
    {
        return validationError switch
        {
            ProxyServerValidationError.NameRequired
            or ProxyServerValidationError.NameTooLong
            or ProxyServerValidationError.NameAlreadyExists => nameof(ProxyServerFormModel.Name),
            ProxyServerValidationError.HostRequired
            or ProxyServerValidationError.HostTooLong
            or ProxyServerValidationError.HostInvalid
            or ProxyServerValidationError.HostAndPortAlreadyExist => nameof(
                ProxyServerFormModel.Host
            ),
            ProxyServerValidationError.PortOutOfRange => nameof(ProxyServerFormModel.Port),
            _ => nameof(ProxyServerFormModel.Username),
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
