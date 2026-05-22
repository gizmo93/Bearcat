namespace Bearcat.Website.Pages.ManageNfoDatabases;

public class RegistrationFormModel
{
    public string? ClassName { get; set; }

    public Dictionary<string, string> Configuration { get; set; } = new();
}
