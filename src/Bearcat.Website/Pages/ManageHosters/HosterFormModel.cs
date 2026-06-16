namespace Bearcat.Website.Pages.ManageHosters;

public class HosterFormModel
{
    public string FullClassName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Configuration { get; set; } = new();

    public bool IsEdit { get; set; }

    public int? HosterRegistrationId { get; set; }

    public int? MaxParallelUploadsOverride { get; set; }
}
