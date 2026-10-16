namespace Bearcat.Website.Pages.FirstStartSetup;

public static class FirstStartSetupChoiceCardStyle
{
    private const string BaseClass =
        "flex flex-col items-start gap-1 rounded-md border p-3 text-left transition-colors focus-visible:outline-2 focus-visible:outline-ring";

    public static string GetClass(bool isSelected)
    {
        return isSelected
            ? $"{BaseClass} border-primary bg-primary/5"
            : $"{BaseClass} border-border/70 hover:bg-muted/40";
    }
}
