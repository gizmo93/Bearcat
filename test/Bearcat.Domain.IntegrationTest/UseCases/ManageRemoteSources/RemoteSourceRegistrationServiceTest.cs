using System.Security.Cryptography;
using System.Text.Json;
using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.RemoteSource;
using Bearcat.Abstractions.RemoteSource.Dto;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageRemoteSources;
using Bearcat.Domain.UseCases.ManageRemoteSources.Sessions;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageRemoteSources;

public class RemoteSourceRegistrationServiceTest : BearcatIntegrationTest
{
    private const string SourceClassName = nameof(FakeRemoteSource);

    private BearcatDbContext dbContext = null!;
    private AesGcmSecretProtector secretProtector = null!;
    private FakeRemoteSource remoteSource = null!;
    private RemoteSourceRegistrationService service = null!;
    private RemoteSourceRegistrationRepository repository = null!;
    private RemoteSourceSessionPool sessionPool = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        var keyProvider = new Mock<IEncryptionKeyProvider>();
        keyProvider
            .Setup(provider => provider.GetKey())
            .Returns(RandomNumberGenerator.GetBytes(32));
        secretProtector = new AesGcmSecretProtector(keyProvider.Object);
        remoteSource = new FakeRemoteSource();

        var factory = new Mock<IRemoteSourceFactory>(MockBehavior.Strict);
        factory.Setup(f => f.GetByClassName(SourceClassName)).Returns(remoteSource);
        factory
            .Setup(f => f.GetRemoteSources())
            .Returns([
                new RemoteSourceDto(
                    "Fake source",
                    SourceClassName,
                    remoteSource.ConfigurationFields
                ),
            ]);

        repository = new RemoteSourceRegistrationRepository(dbContext, dbContext, factory.Object);
        sessionPool = new RemoteSourceSessionPool(NullLogger<RemoteSourceSessionPool>.Instance);
        service = new RemoteSourceRegistrationService(
            repository,
            factory.Object,
            secretProtector,
            new RemoteSourceSessionProvider(sessionPool, factory.Object, secretProtector),
            NullLogger<RemoteSourceRegistrationService>.Instance
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await sessionPool.DisposeAsync();
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidValues_StoresProtectedJsonWithNormalizedTypes()
    {
        // Arrange
        var values = FormValues();

        // Act
        var id = await service.CreateAsync(" Main server ", SourceClassName, values, 3);

        // Assert
        var registration = await dbContext.RemoteSourceRegistrations.SingleAsync();
        registration.Id.ShouldBe(id);
        registration.Name.ShouldBe("Main server");
        registration.SourceClassName.ShouldBe(SourceClassName);
        registration.IsActive.ShouldBeTrue();
        registration.MaxConnections.ShouldBe(3);
        secretProtector.IsProtected(registration.SerializedConfig).ShouldBeTrue();
        registration.SerializedConfig.ShouldNotContain("secret");

        using var document = JsonDocument.Parse(
            secretProtector.Unprotect(registration.SerializedConfig)
        );
        var root = document.RootElement;
        root.GetProperty("Host").GetString().ShouldBe("ftp.example.org");
        root.GetProperty("Port").ValueKind.ShouldBe(JsonValueKind.Number);
        root.GetProperty("Port").GetInt32().ShouldBe(2121);
        root.GetProperty("Password").GetString().ShouldBe("secret");
        root.GetProperty("Mode").GetString().ShouldBe("Secure");
        root.GetProperty("Verify").ValueKind.ShouldBe(JsonValueKind.True);
    }

    [Test]
    public async Task CreateAsync_InvalidValues_ThrowsAndStoresNothing()
    {
        // Arrange
        var values = FormValues();
        values["Mode"] = "Unknown";

        // Act
        var exception = await Should.ThrowAsync<ConfigurationFieldValidationException>(() =>
            service.CreateAsync("Main server", SourceClassName, values)
        );

        // Assert
        exception.Error.ShouldBe(ConfigurationFieldValidationError.InvalidOption);
        (await dbContext.RemoteSourceRegistrations.CountAsync()).ShouldBe(0);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task CreateAsync_MaxConnectionsBelowOne_Throws(int maxConnections)
    {
        // Arrange
        var values = FormValues();

        // Act
        var action = () =>
            service.CreateAsync("Main server", SourceClassName, values, maxConnections);

        // Assert
        var exception = await Should.ThrowAsync<ArgumentException>(action);
        exception.Message.ShouldBe("Max connections must be at least 1.");
    }

    [Test]
    public async Task CreateAsync_EmptyName_Throws()
    {
        // Arrange
        var values = FormValues();

        // Act
        var action = () => service.CreateAsync(" ", SourceClassName, values);

        // Assert
        await Should.ThrowAsync<ArgumentException>(action);
    }

    [Test]
    public async Task UpdateAsync_EmptyPassword_KeepsExistingPassword()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        var values = FormValues();
        values["Host"] = "new.example.org";
        values["Password"] = "";

        // Act
        await service.UpdateAsync(id, "Renamed", values, 5);

        // Assert
        var registration = await ReloadAsync(id);
        registration.Name.ShouldBe("Renamed");
        registration.MaxConnections.ShouldBe(5);
        var config = ReadConfig(registration);
        config["Host"].ShouldBe("new.example.org");
        config["Password"].ShouldBe("secret");
    }

    [Test]
    public async Task UpdateAsync_NewPassword_ReplacesPassword()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        var values = FormValues();
        values["Password"] = "new-secret";

        // Act
        await service.UpdateAsync(id, "Main server", values, 2);

        // Assert
        ReadConfig(await ReloadAsync(id))["Password"].ShouldBe("new-secret");
    }

    [Test]
    public async Task GetConfigValuesWithoutSecretsAsync_RegistrationExists_ReturnsValuesWithoutPassword()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());

        // Act
        var values = await service.GetConfigValuesWithoutSecretsAsync(id);

        // Assert
        values.ShouldNotContainKey("Password");
        values["Host"].ShouldBe("ftp.example.org");
        values["Port"].ShouldBe(2121);
        values["Mode"].ShouldBe("Secure");
        values["Verify"].ShouldBe(true);
    }

    [Test]
    public async Task TestConnectionAsync_SessionListsFolders_ReturnsSuccessWithFolderCount()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        remoteSource.RootFolders =
        [
            new RemoteFolderDto("a", "/a", ModifiedAt: null),
            new RemoteFolderDto("b", "/b", ModifiedAt: null),
        ];

        // Act
        var result = await service.TestConnectionAsync(id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.RootFolderCount.ShouldBe(2);
        remoteSource.OpenedConfig!.ToDictionary()["Password"].ShouldBe("secret");
        remoteSource.ListedPath.ShouldBe("/");
        remoteSource.SessionDisposed.ShouldBeFalse();
    }

    [Test]
    public async Task TestConnectionAsync_CalledTwice_ReusesPooledSession()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());

        // Act
        await service.TestConnectionAsync(id);
        var result = await service.TestConnectionAsync(id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        remoteSource.OpenCount.ShouldBe(1);
        remoteSource.SessionDisposed.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_PooledSessionExists_ClosesSessionAndOpensNewOneWithNewConfig()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        await service.TestConnectionAsync(id);
        var values = FormValues();
        values["Password"] = "changed";

        // Act
        await service.UpdateAsync(id, "Main server", values, 2);
        await service.TestConnectionAsync(id);

        // Assert
        remoteSource.SessionDisposed.ShouldBeTrue();
        remoteSource.OpenCount.ShouldBe(2);
        remoteSource.OpenedConfig!.ToDictionary()["Password"].ShouldBe("changed");
    }

    [Test]
    public async Task TestConnectionAsync_ConnectionFails_ReturnsFailureWithMessage()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        remoteSource.OpenException = new IOException("Connection refused");

        // Act
        var result = await service.TestConnectionAsync(id);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Connection refused");
        result.RootFolderCount.ShouldBe(0);
    }

    [Test]
    public async Task TestConnectionAsync_ListingFails_ReturnsFailureAndDisposesSession()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        remoteSource.ListException = new InvalidOperationException("Permission denied");

        // Act
        var result = await service.TestConnectionAsync(id);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Permission denied");
        remoteSource.SessionDisposed.ShouldBeTrue();
    }

    [Test]
    public async Task ListFoldersAsync_SessionListsFolders_ReturnsFoldersOfRequestedPath()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        remoteSource.RootFolders =
        [
            new RemoteFolderDto("tv", "/incoming/tv", ModifiedAt: null),
            new RemoteFolderDto("movies", "/incoming/movies", ModifiedAt: null),
        ];

        // Act
        var result = await service.ListFoldersAsync(id, "/incoming");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result
            .Folders.Select(folder => folder.FullPath)
            .ShouldBe(["/incoming/tv", "/incoming/movies"]);
        remoteSource.ListedPath.ShouldBe("/incoming");
        remoteSource.SessionDisposed.ShouldBeFalse();
    }

    [Test]
    public async Task ListFoldersAsync_ListingFails_ReturnsFailureWithoutFolders()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());
        remoteSource.ListException = new InvalidOperationException("No such directory");

        // Act
        var result = await service.ListFoldersAsync(id, "/missing");

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("No such directory");
        result.Folders.ShouldBeEmpty();
    }

    [Test]
    public async Task ToggleIsActiveAsync_ActiveRegistration_Deactivates()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());

        // Act
        await service.ToggleIsActiveAsync(id);

        // Assert
        (await ReloadAsync(id)).IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task RemoveAsync_RegistrationExists_DeletesRegistration()
    {
        // Arrange
        var id = await service.CreateAsync("Main server", SourceClassName, FormValues());

        // Act
        await service.RemoveAsync(id);

        // Assert
        (await CreateDbContext().RemoteSourceRegistrations.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task GetAllAsync_Registrations_ReturnsReadModelsWithSourceName()
    {
        // Arrange
        await service.CreateAsync("B server", SourceClassName, FormValues(), 4);
        await service.CreateAsync("A server", SourceClassName, FormValues());

        // Act
        var registrations = await repository.GetAllAsync();

        // Assert
        registrations.Select(r => r.Name).ShouldBe(["A server", "B server"]);
        registrations[0].SourceName.ShouldBe("Fake source");
        registrations[0].SourceClassName.ShouldBe(SourceClassName);
        registrations[0].MaxConnections.ShouldBe(RemoteSourceRegistration.DefaultMaxConnections);
        registrations[1].MaxConnections.ShouldBe(4);
        registrations[1].IsActive.ShouldBeTrue();
    }

    private async Task<RemoteSourceRegistration> ReloadAsync(int id)
    {
        return await CreateDbContext().RemoteSourceRegistrations.SingleAsync(r => r.Id == id);
    }

    private Dictionary<string, object?> ReadConfig(RemoteSourceRegistration registration)
    {
        return remoteSource
            .DeserializeConfig(secretProtector.Unprotect(registration.SerializedConfig))
            .ToDictionary()
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static Dictionary<string, object?> FormValues()
    {
        return new Dictionary<string, object?>
        {
            ["Host"] = " ftp.example.org ",
            ["Port"] = 2121d,
            ["Password"] = "secret",
            ["Mode"] = "Secure",
            ["Verify"] = "true",
        };
    }

    private sealed class FakeRemoteSource : IRemoteSource
    {
        public IReadOnlyList<RemoteFolderDto> RootFolders { get; set; } = [];

        public Exception? OpenException { get; set; }

        public Exception? ListException { get; set; }

        public IRemoteSourceConfig? OpenedConfig { get; private set; }

        public string? ListedPath { get; set; }

        public bool SessionDisposed { get; set; }

        public int OpenCount { get; private set; }

        public string Name => "Fake source";

        public IReadOnlyList<ConfigurationField> ConfigurationFields { get; } =
        [
            new("Host", ConfigurationFieldType.Text, IsRequired: true),
            new("Port", ConfigurationFieldType.Number, IsRequired: true, DefaultValue: 21),
            new("Password", ConfigurationFieldType.Password, IsRequired: true),
            new(
                "Mode",
                ConfigurationFieldType.Select,
                IsRequired: true,
                Options: ["Plain", "Secure"]
            ),
            new("Verify", ConfigurationFieldType.Boolean, IsRequired: false),
        ];

        public IRemoteSourceConfig DeserializeConfig(string serializedConfig)
        {
            using var document = JsonDocument.Parse(serializedConfig);

            return new FakeRemoteSourceConfig(
                document
                    .RootElement.EnumerateObject()
                    .ToDictionary(property => property.Name, property => ToValue(property.Value))
            );
        }

        public Task<IRemoteSourceSession> OpenSessionAsync(
            IRemoteSourceConfig config,
            CancellationToken cancellationToken
        )
        {
            OpenedConfig = config;
            OpenCount++;

            if (OpenException is not null)
            {
                throw OpenException;
            }

            return Task.FromResult<IRemoteSourceSession>(new FakeRemoteSourceSession(this));
        }

        private static object? ToValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetInt32(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => element.GetString(),
                _ => null,
            };
        }
    }

    private sealed class FakeRemoteSourceConfig(IReadOnlyDictionary<string, object?> values)
        : IRemoteSourceConfig
    {
        public IReadOnlyDictionary<string, object?> ToDictionary()
        {
            return values;
        }
    }

    private sealed class FakeRemoteSourceSession(FakeRemoteSource source) : IRemoteSourceSession
    {
        public Task<IReadOnlyList<RemoteFolderDto>> ListFoldersAsync(
            string path,
            CancellationToken cancellationToken
        )
        {
            source.ListedPath = path;

            if (source.ListException is not null)
            {
                throw source.ListException;
            }

            return Task.FromResult(source.RootFolders);
        }

        public Task<IReadOnlyList<RemoteFileDto>> ListFilesRecursiveAsync(
            string folderPath,
            CancellationToken cancellationToken
        )
        {
            throw new NotSupportedException();
        }

        public Task DownloadFileAsync(
            RemoteFileDto file,
            string localFilePath,
            ITransferProgress progress,
            CancellationToken cancellationToken
        )
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            source.SessionDisposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
