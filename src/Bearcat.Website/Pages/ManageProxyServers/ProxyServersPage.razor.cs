using Bearcat.Domain.UseCases.ManageProxyServers;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageProxyServers;

public partial class ProxyServersPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private readonly HashSet<int> testingProxyServerIds = [];
    private IReadOnlyList<ProxyServerReadModel> proxyServers = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadProxyServersAsync();
    }

    private async Task LoadProxyServersAsync()
    {
        proxyServers = await operationRunner.RunAsync(
            (IProxyServerReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await ShowDialogAsync(new ProxyServerFormModel(), L["NewProxyServer"]);
    }

    private async Task ShowEditDialogAsync(ProxyServerReadModel proxyServer)
    {
        var readModel = await operationRunner.RunAsync(
            (IProxyServerReadRepository repository) => repository.GetReadModelAsync(proxyServer.Id)
        );

        if (readModel is null)
        {
            return;
        }

        var formModel = new ProxyServerFormModel
        {
            ProxyServerId = readModel.Id,
            Name = readModel.Name,
            ProxyType = readModel.ProxyType,
            Host = readModel.Host,
            Port = readModel.Port,
            Username = readModel.Username,
            HasStoredPassword = readModel.HasStoredPassword,
            HasUnreadableSecrets = readModel.HasUnreadableSecrets,
        };

        await ShowDialogAsync(formModel, L["EditNamedItem", proxyServer.Name]);
    }

    private async Task ShowDialogAsync(ProxyServerFormModel formModel, string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditProxyServerDialog.FormModel)] = formModel,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditProxyServerDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Description = L["ProxyServerDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadProxyServersAsync();
        }
    }

    private async Task TestConnectionAsync(ProxyServerReadModel proxyServer)
    {
        testingProxyServerIds.Add(proxyServer.Id);

        try
        {
            var result = await operationRunner.RunAsync(
                (ProxyServerService service) => service.TestConnectionAsync(proxyServer.Id)
            );

            if (result.IsSuccess)
            {
                toastService.Success(L["ProxyServerReachable", proxyServer.Name]);
                return;
            }

            toastService.Error(
                L["ProxyServerNotReachable", proxyServer.Name, result.ErrorMessage ?? string.Empty]
            );
        }
        finally
        {
            testingProxyServerIds.Remove(proxyServer.Id);
        }
    }

    private async Task DeleteAsync(ProxyServerReadModel proxyServer)
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", proxyServer.Name],
            L["DeleteProxyServerConfirmation", proxyServer.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        await operationRunner.RunAsync(
            (ProxyServerService service) => service.DeleteAsync(proxyServer.Id)
        );

        toastService.Success(L["ProxyServerDeleted", proxyServer.Name]);
        await LoadProxyServersAsync();
    }
}
