using System.Text.Json.Nodes;
using Shouldly;

namespace Bearcat.Cli.UnitTest;

public class ServiceConfigFileTest
{
    private string temporaryDirectory = string.Empty;
    private string configPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        configPath = Path.Combine(temporaryDirectory, "Bearcat", "config.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Save_FileMissing_CreatesFileWithOwnedFields()
    {
        // Arrange
        var config = new ServiceConfigFile
        {
            Database = { ConnectionString = "Host=localhost" },
            Archivers = { RarPath = "rar.exe", SevenZipPath = "7z.exe" },
            WorkingDirectories = ["D:\\Releases"],
            Urls = "http://127.0.0.1:17208",
        };

        // Act
        config.Save(configPath);

        // Assert
        var savedConfiguration = ReadSavedConfiguration();
        savedConfiguration["Database"]!["ConnectionString"]!
            .GetValue<string>()
            .ShouldBe("Host=localhost");
        savedConfiguration["Archivers"]!["RarPath"]!.GetValue<string>().ShouldBe("rar.exe");
        savedConfiguration["Archivers"]!["SevenZipPath"]!.GetValue<string>().ShouldBe("7z.exe");
        savedConfiguration["WorkingDirectories"]![0]!.GetValue<string>().ShouldBe("D:\\Releases");
        savedConfiguration["Urls"]!.GetValue<string>().ShouldBe("http://127.0.0.1:17208");
        savedConfiguration.ContainsKey("ReleaseDataDirectory").ShouldBeFalse();
    }

    [Test]
    public void Save_ExistingFileWithUnknownSections_KeepsUnknownSections()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "ConnectionString": "Host=old", "CommandTimeout": 60 },
              "Urls": "http://127.0.0.1:17208",
              "Bearcat": { "ApiKey": "secret-key" },
              "Logging": { "LogLevel": { "Default": "Information" } }
            }
            """
        );
        var config = ServiceConfigFile.Load(configPath);
        config.Database.ConnectionString = "Host=new";

        // Act
        config.Save(configPath);

        // Assert
        var savedConfiguration = ReadSavedConfiguration();
        savedConfiguration["Bearcat"]!["ApiKey"]!.GetValue<string>().ShouldBe("secret-key");
        savedConfiguration["Logging"]!["LogLevel"]!["Default"]!
            .GetValue<string>()
            .ShouldBe("Information");
        savedConfiguration["Database"]!["CommandTimeout"]!.GetValue<int>().ShouldBe(60);
    }

    [Test]
    public void Save_ExistingFile_UpdatesOwnedFields()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "ConnectionString": "Host=old" },
              "Archivers": { "RarPath": "old-rar", "SevenZipPath": "old-7z" },
              "WorkingDirectories": [ "C:\\Old" ],
              "Urls": "http://127.0.0.1:17208",
              "Bearcat": { "ApiKey": "secret-key" }
            }
            """
        );
        var config = new ServiceConfigFile
        {
            Database = { ConnectionString = "Host=new" },
            Archivers = { RarPath = "new-rar", SevenZipPath = "new-7z" },
            WorkingDirectories = ["D:\\New", "E:\\Second"],
            Urls = "http://127.0.0.1:18000",
        };

        // Act
        config.Save(configPath);

        // Assert
        var savedConfiguration = ReadSavedConfiguration();
        savedConfiguration["Database"]!["ConnectionString"]!
            .GetValue<string>()
            .ShouldBe("Host=new");
        savedConfiguration["Archivers"]!["RarPath"]!.GetValue<string>().ShouldBe("new-rar");
        savedConfiguration["Archivers"]!["SevenZipPath"]!.GetValue<string>().ShouldBe("new-7z");
        savedConfiguration["WorkingDirectories"]!
            .AsArray()
            .Select(directory => directory!.GetValue<string>())
            .ShouldBe(["D:\\New", "E:\\Second"]);
        savedConfiguration["Urls"]!.GetValue<string>().ShouldBe("http://127.0.0.1:18000");
        savedConfiguration["Bearcat"]!["ApiKey"]!.GetValue<string>().ShouldBe("secret-key");
    }

    [Test]
    public void Save_ExistingFileWithReleaseDataDirectory_WritesWorkingDirectoriesInstead()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "ConnectionString": "Host=localhost" },
              "ReleaseDataDirectory": "D:\\Releases",
              "Urls": "http://127.0.0.1:17208"
            }
            """
        );
        var config = ServiceConfigFile.Load(configPath);

        // Act
        config.Save(configPath);

        // Assert
        var savedConfiguration = ReadSavedConfiguration();
        savedConfiguration.ContainsKey("ReleaseDataDirectory").ShouldBeFalse();
        savedConfiguration["WorkingDirectories"]![0]!.GetValue<string>().ShouldBe("D:\\Releases");
    }

    private void WriteExistingConfiguration(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, json);
    }

    private JsonObject ReadSavedConfiguration()
    {
        return JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
    }
}
