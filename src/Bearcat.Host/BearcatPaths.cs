namespace Bearcat.Host;

public static class BearcatPaths
{
    public static string WindowsServiceConfigPath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Bearcat",
            "config.json"
        );
}
