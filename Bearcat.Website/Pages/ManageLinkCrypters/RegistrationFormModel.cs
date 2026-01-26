namespace Bearcat.Website.Pages.ManageLinkCrypters;

public class RegistrationFormModel
{
    public string? Name { get; set; }

    public string? ClassName { get; set; }

    public Dictionary<string, string> Configuration { get; set; } = new();
}
