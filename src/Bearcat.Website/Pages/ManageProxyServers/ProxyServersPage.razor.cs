using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.UseCases.ManageProxyServers;
using Bearcat.Domain.UseCases.ManageProxyServers.CategoryDefaults;
using Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;
using Bearcat.Domain.UseCases.ManageProxyServers.Deletion;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;

namespace Bearcat.Website.Pages.ManageProxyServers;

public partial class ProxyServersPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private const int DirectConnectionOptionValue = 0;

    private readonly HashSet<int> testingProxyServerIds = [];
    private IReadOnlyList<ProxyServerReadModel> proxyServers = [];
    private Dictionary<ProxyCategory, int?> proxyServerIdByCategory = [];
    private int allCategoriesOptionValue = DirectConnectionOptionValue;
    private bool isSavingCategoryDefaults;

    private static IReadOnlyList<ProxyCategory> ProxyCategories { get; } =
        Enum.GetValues<ProxyCategory>();

    private IReadOnlyList<SelectOption<int>> ProxyServerOptions =>
        [
            new(DirectConnectionOptionValue, L["DirectConnection"]),
            .. proxyServers.Select(proxyServer => new SelectOption<int>(
                proxyServer.Id,
                proxyServer.Name
            )),
        ];

    protected override async Task OnInitializedAsync()
    {
        await LoadProxyServersAsync();
        await LoadCategoryDefaultsAsync();
    }

    private async Task LoadProxyServersAsync()
    {
        proxyServers = await operationRunner.RunAsync(
            (IProxyServerReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task LoadCategoryDefaultsAsync()
    {
        var categoryDefaults = await operationRunner.RunAsync(
            (IProxyCategoryDefaultReadRepository repository) => repository.GetAllAsync()
        );

        proxyServerIdByCategory = categoryDefaults.ToDictionary(
            categoryDefault => categoryDefault.ProxyCategory,
            categoryDefault => categoryDefault.ProxyServerId
        );
    }

    private int GetSelectedOptionValue(ProxyCategory category)
    {
        return proxyServerIdByCategory.GetValueOrDefault(category) ?? DirectConnectionOptionValue;
    }

    private async Task SetCategoryDefaultAsync(ProxyCategory category, int optionValue)
    {
        isSavingCategoryDefaults = true;

        try
        {
            await operationRunner.RunAsync(
                (ProxyCategoryDefaultService service) =>
                    service.SetDefaultAsync(category, ToProxyServerId(optionValue))
            );

            await LoadCategoryDefaultsAsync();
            toastService.Success(
                L["ProxyCategoryDefaultSaved", L.Localize(category), GetOptionName(optionValue)]
            );
        }
        finally
        {
            isSavingCategoryDefaults = false;
        }
    }

    private async Task ApplyToAllCategoriesAsync()
    {
        isSavingCategoryDefaults = true;

        try
        {
            await operationRunner.RunAsync(
                (ProxyCategoryDefaultService service) =>
                    service.SetDefaultForAllCategoriesAsync(
                        ToProxyServerId(allCategoriesOptionValue)
                    )
            );

            await LoadCategoryDefaultsAsync();
            toastService.Success(
                L["ProxyCategoryDefaultsAppliedToAll", GetOptionName(allCategoriesOptionValue)]
            );
        }
        finally
        {
            isSavingCategoryDefaults = false;
        }
    }

    private static int? ToProxyServerId(int optionValue)
    {
        return optionValue == DirectConnectionOptionValue ? null : optionValue;
    }

    private string GetOptionName(int optionValue)
    {
        return ProxyServerOptions.First(option => option.Value == optionValue).Text;
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

            var message = L[$"ProxyServerConnectionTestOutcome.{result.Outcome}", proxyServer.Name];

            if (result.IsSuccess)
            {
                toastService.Success(message);
                return;
            }

            toastService.Error(
                result.TechnicalDetail is null
                    ? message
                    : L[
                        "ProxyServerConnectionTestFailedWithDetail",
                        message,
                        result.TechnicalDetail
                    ]
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

        var result = await operationRunner.RunAsync(
            (ProxyServerService service) => service.DeleteAsync(proxyServer.Id)
        );

        if (!result.IsDeleted)
        {
            toastService.Error(DescribeUsage(proxyServer.Name, result));
            return;
        }

        toastService.Success(L["ProxyServerDeleted", proxyServer.Name]);
        await LoadProxyServersAsync();
    }

    private string DescribeUsage(string proxyServerName, ProxyServerDeleteResult result)
    {
        var categoryNames = string.Join(
            ", ",
            result.UsingCategoryDefaults.Select(category => L.Localize(category))
        );

        return $"{L["ProxyServerStillInUse", proxyServerName]} {L["ProxyServerUsedAsCategoryDefault", categoryNames]}";
    }
}
