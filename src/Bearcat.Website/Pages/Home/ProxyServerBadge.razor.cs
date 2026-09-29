using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.Home;

public partial class ProxyServerBadge : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<string> ProxyServerNames { get; set; } = null!;

    private string JoinedProxyServerNames => string.Join(", ", ProxyServerNames);
}
