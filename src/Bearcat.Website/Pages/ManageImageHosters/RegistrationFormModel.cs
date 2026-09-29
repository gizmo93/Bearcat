using Bearcat.Abstractions.Proxies;

namespace Bearcat.Website.Pages.ManageImageHosters;

public class RegistrationFormModel
{
    public string? Name { get; set; }

    public string? ClassName { get; set; }

    public Dictionary<string, string> Configuration { get; set; } = new();

    public ProxySelection ProxySelection { get; set; } = ProxySelection.UseCategoryDefault;

    public int? ProxyServerId { get; set; }
}
