using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home.Activity;

public partial class ProxyServerChip : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<string> ProxyServerNames { get; set; } = null!;

    private string JoinedProxyServerNames => string.Join(", ", ProxyServerNames);
}
