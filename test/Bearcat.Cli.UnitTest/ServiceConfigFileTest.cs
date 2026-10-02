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

    [Test]
    public void Load_FileWithoutProvider_UsesPostgres()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "ConnectionString": "Host=localhost" },
              "Urls": "http://127.0.0.1:17208"
            }
            """
        );

        // Act
        var config = ServiceConfigFile.Load(configPath);

        // Assert
        config.Database.Provider.ShouldBeNull();
        config.Database.EffectiveProvider.ShouldBe(DatabaseProvider.Postgres);
        config.Database.ConnectionString.ShouldBe("Host=localhost");
        config.Database.SqliteFilePath.ShouldBeNull();
    }

    [Test]
    public void Save_LoadedFileWithoutProvider_DoesNotAddProvider()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "ConnectionString": "Host=old" },
              "Urls": "http://127.0.0.1:17208"
            }
            """
        );
        var config = ServiceConfigFile.Load(configPath);
        config.Database.ConnectionString = "Host=new";

        // Act
        config.Save(configPath);

        // Assert
        var savedDatabaseSection = ReadSavedConfiguration()["Database"]!.AsObject();
        savedDatabaseSection.ContainsKey("Provider").ShouldBeFalse();
        savedDatabaseSection.ContainsKey("SqliteFilePath").ShouldBeFalse();
        savedDatabaseSection["ConnectionString"]!.GetValue<string>().ShouldBe("Host=new");
    }

    [Test]
    public void Save_SqliteConfiguration_WritesProviderAndFilePathWithoutConnectionString()
    {
        // Arrange
        var config = new ServiceConfigFile
        {
            Database =
            {
                Provider = DatabaseProvider.Sqlite,
                SqliteFilePath = "C:\\ProgramData\\Bearcat\\bearcat.db",
            },
            WorkingDirectories = ["D:\\Releases"],
            Urls = "http://127.0.0.1:17208",
        };

        // Act
        config.Save(configPath);

        // Assert
        var savedDatabaseSection = ReadSavedConfiguration()["Database"]!.AsObject();
        savedDatabaseSection["Provider"]!.GetValue<string>().ShouldBe("Sqlite");
        savedDatabaseSection["SqliteFilePath"]!
            .GetValue<string>()
            .ShouldBe("C:\\ProgramData\\Bearcat\\bearcat.db");
        savedDatabaseSection.ContainsKey("ConnectionString").ShouldBeFalse();
    }

    [Test]
    public void Load_SavedSqliteConfiguration_ReadsProviderAndFilePath()
    {
        // Arrange
        new ServiceConfigFile
        {
            Database =
            {
                Provider = DatabaseProvider.Sqlite,
                SqliteFilePath = "C:\\ProgramData\\Bearcat\\bearcat.db",
            },
            Urls = "http://127.0.0.1:17208",
        }.Save(configPath);

        // Act
        var config = ServiceConfigFile.Load(configPath);

        // Assert
        config.Database.Provider.ShouldBe(DatabaseProvider.Sqlite);
        config.Database.EffectiveProvider.ShouldBe(DatabaseProvider.Sqlite);
        config.Database.SqliteFilePath.ShouldBe("C:\\ProgramData\\Bearcat\\bearcat.db");
        config.Database.ConnectionString.ShouldBeNull();
    }

    [Test]
    public void Save_SqliteConfigurationOverExistingPostgresFile_RemovesConnectionString()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "ConnectionString": "Host=old", "CommandTimeout": 60 },
              "Urls": "http://127.0.0.1:17208"
            }
            """
        );
        var config = new ServiceConfigFile
        {
            Database =
            {
                Provider = DatabaseProvider.Sqlite,
                SqliteFilePath = "C:\\ProgramData\\Bearcat\\bearcat.db",
            },
            Urls = "http://127.0.0.1:17208",
        };

        // Act
        config.Save(configPath);

        // Assert
        var savedDatabaseSection = ReadSavedConfiguration()["Database"]!.AsObject();
        savedDatabaseSection["Provider"]!.GetValue<string>().ShouldBe("Sqlite");
        savedDatabaseSection.ContainsKey("ConnectionString").ShouldBeFalse();
        savedDatabaseSection["CommandTimeout"]!.GetValue<int>().ShouldBe(60);
    }

    [Test]
    public void Save_PostgresConfigurationOverExistingSqliteFile_RemovesSqliteFilePath()
    {
        // Arrange
        WriteExistingConfiguration(
            """
            {
              "Database": { "Provider": "Sqlite", "SqliteFilePath": "C:\\Old\\bearcat.db" },
              "Urls": "http://127.0.0.1:17208"
            }
            """
        );
        var config = new ServiceConfigFile
        {
            Database = { Provider = DatabaseProvider.Postgres, ConnectionString = "Host=new" },
            Urls = "http://127.0.0.1:17208",
        };

        // Act
        config.Save(configPath);

        // Assert
        var savedDatabaseSection = ReadSavedConfiguration()["Database"]!.AsObject();
        savedDatabaseSection["Provider"]!.GetValue<string>().ShouldBe("Postgres");
        savedDatabaseSection["ConnectionString"]!.GetValue<string>().ShouldBe("Host=new");
        savedDatabaseSection.ContainsKey("SqliteFilePath").ShouldBeFalse();
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
