using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Bearcat.Desktop;

public static class BearcatHostResolver
{
    public static ResolvedBearcatHost Resolve(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!File.Exists(configuredPath))
            {
                throw new FileNotFoundException(
                    "Configured Bearcat.Host path was not found.",
                    configuredPath
                );
            }

            return Path.GetExtension(configuredPath)
                .Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                ? ResolveProjectOutput(configuredPath)
                : ResolvedBearcatHost.FromFile(
                    configuredPath,
                    ShouldUseDevelopmentEnvironment(configuredPath)
                );
        }

        var baseDirectory = AppContext.BaseDirectory;
        var executableName = OperatingSystem.IsWindows() ? "Bearcat.Host.exe" : "Bearcat.Host";

        var existingDllPath = new[]
            {
                Path.Combine(baseDirectory, executableName), Path.Combine(baseDirectory, "Bearcat.Host.dll"),
            }
            .FirstOrDefault(File.Exists);

        if (existingDllPath is not null)
        {
            return ResolvedBearcatHost.FromFile(
                path: existingDllPath,
                useDevelopmentEnvironment: ShouldUseDevelopmentEnvironment(existingDllPath)
            );
        }

        var currentDirectory = new DirectoryInfo(baseDirectory);
        while (currentDirectory is not null)
        {
            var projectPath = Path.Combine(
                currentDirectory.FullName,
                "src",
                "Bearcat.Host",
                "Bearcat.Host.csproj"
            );

            if (File.Exists(projectPath))
            {
                return ResolveProjectOutput(projectPath);
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new FileNotFoundException(
            "Bearcat.Host could not be auto-detected. Choose a published Bearcat.Host executable."
        );
    }

    private static ResolvedBearcatHost ResolveProjectOutput(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var executableName = OperatingSystem.IsWindows() ? "Bearcat.Host.exe" : "Bearcat.Host";

        var candidates = new[]
        {
            Path.Combine(projectDirectory, "bin", "Release", "net10.0", executableName),
            Path.Combine(projectDirectory, "bin", "Release", "net10.0", "Bearcat.Host.dll"),
            Path.Combine(projectDirectory, "bin", "Debug", "net10.0", executableName),
            Path.Combine(projectDirectory, "bin", "Debug", "net10.0", "Bearcat.Host.dll"),
        }
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        if (candidates.Count > 0)
        {
            return ResolvedBearcatHost.FromFile(
                path: candidates[0],
                useDevelopmentEnvironment: true
            );
        }

        throw new FileNotFoundException(
            "Bearcat.Host project was found, but no build output exists. Build Bearcat.Host first.",
            projectPath
        );
    }

    private static bool ShouldUseDevelopmentEnvironment(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;

        if (directory.Split(Path.DirectorySeparatorChar).Contains("publish"))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, "Bearcat.Host.staticwebassets.runtime.json"))
            && !Directory.Exists(Path.Combine(directory, "wwwroot", "_content"));
    }
}

public sealed class ResolvedBearcatHost
{
    private ResolvedBearcatHost(string path, bool useDevelopmentEnvironment)
    {
        this.path = path;
        UseDevelopmentEnvironment = useDevelopmentEnvironment;
    }

    public bool UseDevelopmentEnvironment { get; }

    private readonly string path;

    public static ResolvedBearcatHost FromFile(string path, bool useDevelopmentEnvironment = false)
    {
        return new ResolvedBearcatHost(path, useDevelopmentEnvironment);
    }

    public ProcessStartInfo CreateStartInfo()
    {
        if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = Quote(path),
                WorkingDirectory = Path.GetDirectoryName(path)!,
            };
        }

        return new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
        };
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}
