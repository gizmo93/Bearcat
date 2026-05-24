namespace Bearcat.Desktop;

public enum TrayAppStatus
{
    Stopped,
    Working,
    Running,
}

public static class TrayAppStatusExtensions
{
    public static string ToDisplayText(this TrayAppStatus status)
    {
        return status switch
        {
            TrayAppStatus.Running => "Running",
            TrayAppStatus.Working => "Working...",
            _ => "Stopped",
        };
    }
}
