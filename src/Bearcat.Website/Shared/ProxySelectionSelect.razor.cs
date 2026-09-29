using Bearcat.Abstractions.Proxies;
using Bearcat.Domain.UseCases.ManageProxyServers.ReadModels;
using Bearcat.Domain.UseCases.ManageProxyServers.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Shared;

public partial class ProxySelectionSelect(IScopedOperationRunner operationRunner) : ComponentBase
{
    private const int CategoryDefaultOptionValue = -1;
    private const int NoProxyOptionValue = 0;

    [Parameter]
    [EditorRequired]
    public string Label { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public ProxyCategory ProxyCategory { get; set; }

    [Parameter]
    public ProxySelection ProxySelection { get; set; }

    [Parameter]
    public EventCallback<ProxySelection> ProxySelectionChanged { get; set; }

    [Parameter]
    public int? ProxyServerId { get; set; }

    [Parameter]
    public EventCallback<int?> ProxyServerIdChanged { get; set; }

    [Parameter]
    public string? HelperText { get; set; }

    private IReadOnlyList<ProxyServerReadModel> proxyServers = [];
    private int? categoryDefaultProxyServerId;
    private bool isLoaded;

    private IReadOnlyList<SelectOption<int>> Options =>
        [
            new(CategoryDefaultOptionValue, L["ProxyCategoryDefaultOption", CategoryDefaultName]),
            new(NoProxyOptionValue, L["NoProxyDirectConnection"]),
            .. proxyServers.Select(proxyServer => new SelectOption<int>(
                proxyServer.Id,
                proxyServer.Name
            )),
        ];

    private string CategoryDefaultName =>
        categoryDefaultProxyServerId is null
            ? L["DirectConnection"]
            : proxyServers
                .First(proxyServer => proxyServer.Id == categoryDefaultProxyServerId)
                .Name;

    private int SelectedOptionValue =>
        ProxySelection switch
        {
            ProxySelection.UseCategoryDefault => CategoryDefaultOptionValue,
            ProxySelection.NoProxy => NoProxyOptionValue,
            _ => ProxyServerId!.Value,
        };

    protected override async Task OnInitializedAsync()
    {
        proxyServers = await operationRunner.RunAsync(
            (IProxyServerReadRepository repository) => repository.GetAllAsync()
        );

        var categoryDefaults = await operationRunner.RunAsync(
            (IProxyCategoryDefaultReadRepository repository) => repository.GetAllAsync()
        );

        categoryDefaultProxyServerId = categoryDefaults
            .FirstOrDefault(categoryDefault => categoryDefault.ProxyCategory == ProxyCategory)
            ?.ProxyServerId;

        isLoaded = true;
    }

    private async Task SelectOptionAsync(int optionValue)
    {
        var (proxySelection, proxyServerId) = optionValue switch
        {
            CategoryDefaultOptionValue => (ProxySelection.UseCategoryDefault, (int?)null),
            NoProxyOptionValue => (ProxySelection.NoProxy, null),
            _ => (ProxySelection.SpecificProxyServer, optionValue),
        };

        await ProxySelectionChanged.InvokeAsync(proxySelection);
        await ProxyServerIdChanged.InvokeAsync(proxyServerId);
    }
}
