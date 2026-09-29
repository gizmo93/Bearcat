using Bearcat.Abstractions.Proxies;
using Bearcat.Abstractions.Security;
using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets;
using Bearcat.Domain.UseCases.DetectUnreadableSecrets.Repositories;
using Bearcat.Domain.UseCases.ManageProxyServers;
using Bearcat.Domain.UseCases.ManageProxyServers.ConnectionTest;
using Bearcat.Domain.UseCases.ManageProxyServers.Dto;
using Bearcat.Domain.UseCases.ManageProxyServers.Validation;
using Bearcat.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageProxyServers;

public class ProxyServerServiceTest
{
    private const string StoredEncryptedPassword = "protected:stored";

    private FakeProxyServerWriteRepository repository = null!;
    private Mock<IProxyServerConnectionTester> connectionTesterMock = null!;
    private Mock<IProxyRoutingCache> proxyRoutingCacheMock = null!;
    private Mock<IUnreadableSecretsRepository> unreadableSecretsRepositoryMock = null!;
    private Mock<INotificationService> notificationServiceMock = null!;
    private ProxyServerService service = null!;

    [SetUp]
    public void SetUp()
    {
        repository = new FakeProxyServerWriteRepository();
        connectionTesterMock = new Mock<IProxyServerConnectionTester>();
        proxyRoutingCacheMock = new Mock<IProxyRoutingCache>();
        unreadableSecretsRepositoryMock = new Mock<IUnreadableSecretsRepository>();
        notificationServiceMock = new Mock<INotificationService>();

        var secretProtectorMock = new Mock<ISecretProtector>();
        secretProtectorMock
            .Setup(protector => protector.Protect(It.IsAny<string>()))
            .Returns((string plaintext) => $"protected:{plaintext}");
        secretProtectorMock
            .Setup(protector => protector.Unprotect(It.IsAny<string>()))
            .Returns((string protectedValue) => protectedValue["protected:".Length..]);

        service = new ProxyServerService(
            repository,
            secretProtectorMock.Object,
            connectionTesterMock.Object,
            proxyRoutingCacheMock.Object,
            new UnreadableSecretsNotificationService(
                unreadableSecretsRepositoryMock.Object,
                notificationServiceMock.Object
            ),
            NullLogger<ProxyServerService>.Instance
        );
    }

    [Test]
    public async Task CreateAsync_ValidInputWithCredentials_PersistsTrimmedValuesAndEncryptedPassword()
    {
        // Act
        var result = await service.CreateAsync(
            new ProxyServerInput(
                "  Upload proxy  ",
                ProxyType.Socks5,
                "  proxy.example.com  ",
                1080,
                "  alice  ",
                " secret ",
                RemoveStoredPassword: false
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var proxyServer = repository.ProxyServers.ShouldHaveSingleItem();
        result.ProxyServerId.ShouldBe(proxyServer.Id);
        proxyServer.Name.ShouldBe("Upload proxy");
        proxyServer.ProxyType.ShouldBe(ProxyType.Socks5);
        proxyServer.Host.ShouldBe("proxy.example.com");
        proxyServer.Port.ShouldBe(1080);
        proxyServer.Username.ShouldBe("alice");
        proxyServer.EncryptedPassword.ShouldBe("protected: secret ");
        repository.SaveChangesCallCount.ShouldBe(1);
    }

    [Test]
    public async Task CreateAsync_EmptyUsernameAndPassword_PersistsProxyServerWithoutCredentials()
    {
        // Act
        var result = await service.CreateAsync(CreateInput(username: "  ", password: ""));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var proxyServer = repository.ProxyServers.ShouldHaveSingleItem();
        proxyServer.Username.ShouldBeNull();
        proxyServer.EncryptedPassword.ShouldBeNull();
    }

    [TestCase("proxy.example.com")]
    [TestCase("localhost")]
    [TestCase("10.0.0.1")]
    [TestCase("::1")]
    public async Task CreateAsync_ValidHost_Succeeds(string host)
    {
        // Act
        var result = await service.CreateAsync(CreateInput(host: host));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [TestCase("", ProxyServerValidationError.NameRequired)]
    [TestCase("   ", ProxyServerValidationError.NameRequired)]
    public async Task CreateAsync_InvalidName_ReturnsValidationError(
        string name,
        ProxyServerValidationError expectedError
    )
    {
        // Act
        var result = await service.CreateAsync(CreateInput(name: name));

        // Assert
        result.ValidationErrors.ShouldBe([expectedError]);
        repository.ProxyServers.ShouldBeEmpty();
        repository.SaveChangesCallCount.ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_NameLongerThan100Characters_ReturnsNameTooLong()
    {
        // Act
        var result = await service.CreateAsync(CreateInput(name: new string('a', 101)));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.NameTooLong]);
    }

    [Test]
    public async Task CreateAsync_NameAlreadyExists_ReturnsNameAlreadyExistsAndDoesNotSave()
    {
        // Arrange
        AddStoredProxyServer(name: "Upload proxy", port: 3128);

        // Act
        var result = await service.CreateAsync(CreateInput(name: " Upload proxy "));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.NameAlreadyExists]);
        repository.ProxyServers.Count.ShouldBe(1);
        repository.SaveChangesCallCount.ShouldBe(0);
    }

    [TestCase("", ProxyServerValidationError.HostRequired)]
    [TestCase("http://proxy.example.com", ProxyServerValidationError.HostInvalid)]
    [TestCase("proxy.example.com:8080", ProxyServerValidationError.HostInvalid)]
    [TestCase("proxy example", ProxyServerValidationError.HostInvalid)]
    public async Task CreateAsync_InvalidHost_ReturnsValidationError(
        string host,
        ProxyServerValidationError expectedError
    )
    {
        // Act
        var result = await service.CreateAsync(CreateInput(host: host));

        // Assert
        result.ValidationErrors.ShouldBe([expectedError]);
    }

    [Test]
    public async Task CreateAsync_HostLongerThan255Characters_ReturnsHostTooLong()
    {
        // Act
        var result = await service.CreateAsync(CreateInput(host: new string('a', 256)));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.HostTooLong]);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(65536)]
    public async Task CreateAsync_PortOutOfRange_ReturnsPortOutOfRange(int port)
    {
        // Act
        var result = await service.CreateAsync(CreateInput(port: port));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.PortOutOfRange]);
    }

    [Test]
    public async Task CreateAsync_UsernameLongerThan255Characters_ReturnsUsernameTooLong()
    {
        // Act
        var result = await service.CreateAsync(CreateInput(username: new string('a', 256)));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.UsernameTooLong]);
    }

    [Test]
    public async Task CreateAsync_PasswordWithoutUsername_ReturnsUsernameRequiredForPassword()
    {
        // Act
        var result = await service.CreateAsync(CreateInput(username: null, password: "secret"));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.UsernameRequiredForPassword]);
    }

    [Test]
    public async Task CreateAsync_SeveralInvalidValues_ReturnsAllValidationErrors()
    {
        // Act
        var result = await service.CreateAsync(CreateInput(name: "", host: "", port: 0));

        // Assert
        result.ValidationErrors.ShouldBe([
            ProxyServerValidationError.NameRequired,
            ProxyServerValidationError.HostRequired,
            ProxyServerValidationError.PortOutOfRange,
        ]);
    }

    [Test]
    public async Task UpdateAsync_ValidInput_UpdatesValues()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();

        // Act
        var result = await service.UpdateAsync(
            proxyServer.Id,
            CreateInput(
                name: " Renamed proxy ",
                proxyType: ProxyType.Socks5,
                host: " 10.0.0.2 ",
                port: 1080,
                username: " bob "
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ProxyServerId.ShouldBe(proxyServer.Id);
        proxyServer.Name.ShouldBe("Renamed proxy");
        proxyServer.ProxyType.ShouldBe(ProxyType.Socks5);
        proxyServer.Host.ShouldBe("10.0.0.2");
        proxyServer.Port.ShouldBe(1080);
        proxyServer.Username.ShouldBe("bob");
        repository.SaveChangesCallCount.ShouldBe(1);
    }

    [Test]
    public async Task UpdateAsync_UnchangedOwnName_DoesNotReportNameAlreadyExists()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(name: "Upload proxy");

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(name: "Upload proxy"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_NameOfOtherProxyServer_ReturnsNameAlreadyExistsAndKeepsValues()
    {
        // Arrange
        AddStoredProxyServer(name: "Other proxy", port: 3128);
        var proxyServer = AddStoredProxyServer(name: "Upload proxy");

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(name: "Other proxy"));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.NameAlreadyExists]);
        proxyServer.Name.ShouldBe("Upload proxy");
        repository.SaveChangesCallCount.ShouldBe(0);
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task UpdateAsync_EmptyPassword_KeepsStoredPassword(string? password)
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(encryptedPassword: StoredEncryptedPassword);

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(password: password));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        proxyServer.EncryptedPassword.ShouldBe(StoredEncryptedPassword);
    }

    [Test]
    public async Task UpdateAsync_NewPassword_ReplacesStoredPasswordAndClearsUnreadableFlag()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(
            encryptedPassword: StoredEncryptedPassword,
            hasUnreadableSecrets: true
        );

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(password: "new"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        proxyServer.EncryptedPassword.ShouldBe("protected:new");
        proxyServer.HasUnreadableSecrets.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_RemoveStoredPassword_ClearsPasswordAndUnreadableFlag()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(
            encryptedPassword: StoredEncryptedPassword,
            hasUnreadableSecrets: true
        );

        // Act
        var result = await service.UpdateAsync(
            proxyServer.Id,
            CreateInput(removeStoredPassword: true)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        proxyServer.EncryptedPassword.ShouldBeNull();
        proxyServer.HasUnreadableSecrets.ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_RemoveStoredPasswordWithNewPassword_StoresNewPassword()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(encryptedPassword: StoredEncryptedPassword);

        // Act
        var result = await service.UpdateAsync(
            proxyServer.Id,
            CreateInput(password: "new", removeStoredPassword: true)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        proxyServer.EncryptedPassword.ShouldBe("protected:new");
    }

    [Test]
    public async Task UpdateAsync_EmptyPasswordWithUnreadableStoredPassword_KeepsUnreadableFlag()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(
            encryptedPassword: StoredEncryptedPassword,
            hasUnreadableSecrets: true
        );

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(password: null));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        proxyServer.EncryptedPassword.ShouldBe(StoredEncryptedPassword);
        proxyServer.HasUnreadableSecrets.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_UsernameRemovedWhileStoredPasswordIsKept_ReturnsUsernameRequiredForPassword()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(encryptedPassword: StoredEncryptedPassword);

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(username: null));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.UsernameRequiredForPassword]);
        proxyServer.Username.ShouldBe("alice");
    }

    [Test]
    public async Task UpdateAsync_UsernameRemovedTogetherWithStoredPassword_RemovesCredentials()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(encryptedPassword: StoredEncryptedPassword);

        // Act
        var result = await service.UpdateAsync(
            proxyServer.Id,
            CreateInput(username: null, removeStoredPassword: true)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        proxyServer.Username.ShouldBeNull();
        proxyServer.EncryptedPassword.ShouldBeNull();
    }

    [Test]
    public async Task UpdateAsync_UnreadableSecretsNotificationWithoutRemainingUnreadableSecrets_ResolvesNotification()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(
            encryptedPassword: StoredEncryptedPassword,
            hasUnreadableSecrets: true
        );
        unreadableSecretsRepositoryMock
            .Setup(unreadableSecretsRepository =>
                unreadableSecretsRepository.AnyUnresolvedUnreadableSecretsNotificationAsync(
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        // Act
        await service.UpdateAsync(proxyServer.Id, CreateInput(password: "new"));

        // Assert
        notificationServiceMock.Verify(
            notificationService =>
                notificationService.ResolveAllOfKindAsync(
                    NotificationKind.UnreadableSecretsDetected,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [TestCase("proxy.example.com", 8080)]
    [TestCase("PROXY.example.com", 8080)]
    [TestCase(" proxy.example.com ", 8080)]
    public async Task CreateAsync_HostAndPortOfOtherProxyServer_ReturnsHostAndPortAlreadyExist(
        string host,
        int port
    )
    {
        // Arrange
        AddStoredProxyServer(name: "Other proxy");

        // Act
        var result = await service.CreateAsync(CreateInput(host: host, port: port));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.HostAndPortAlreadyExist]);
        repository.SaveChangesCallCount.ShouldBe(0);
    }

    [Test]
    public async Task CreateAsync_SameHostWithOtherPort_Succeeds()
    {
        // Arrange
        AddStoredProxyServer(name: "Other proxy");

        // Act
        var result = await service.CreateAsync(CreateInput(port: 3128));

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_UnchangedOwnHostAndPort_DoesNotReportHostAndPortAlreadyExist()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput());

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Test]
    public async Task UpdateAsync_HostAndPortOfOtherProxyServer_ReturnsHostAndPortAlreadyExist()
    {
        // Arrange
        AddStoredProxyServer(name: "Other proxy", port: 3128);
        var proxyServer = AddStoredProxyServer();

        // Act
        var result = await service.UpdateAsync(proxyServer.Id, CreateInput(port: 3128));

        // Assert
        result.ValidationErrors.ShouldBe([ProxyServerValidationError.HostAndPortAlreadyExist]);
        proxyServer.Port.ShouldBe(8080);
    }

    [Test]
    public async Task CreateAsync_ValidInput_RefreshesProxyRoutingCache()
    {
        // Act
        await service.CreateAsync(CreateInput());

        // Assert
        proxyRoutingCacheMock.Verify(
            cache => cache.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task UpdateAsync_ValidInput_RefreshesProxyRoutingCache()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();

        // Act
        await service.UpdateAsync(proxyServer.Id, CreateInput(name: "Renamed proxy"));

        // Assert
        proxyRoutingCacheMock.Verify(
            cache => cache.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteAsync_UnusedProxyServer_RemovesProxyServerAndRefreshesProxyRoutingCache()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();

        // Act
        var result = await service.DeleteAsync(proxyServer.Id);

        // Assert
        result.IsDeleted.ShouldBeTrue();
        result.UsingCategoryDefaults.ShouldBeEmpty();
        repository.ProxyServers.ShouldBeEmpty();
        repository.SaveChangesCallCount.ShouldBe(1);
        proxyRoutingCacheMock.Verify(
            cache => cache.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task DeleteAsync_ProxyServerUsedAsCategoryDefault_ReturnsUsingCategoriesAndKeepsProxyServer()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();
        repository.CategoryDefaultsByProxyServerId[proxyServer.Id] =
        [
            ProxyCategory.HosterUploads,
            ProxyCategory.ImageHosters,
        ];

        // Act
        var result = await service.DeleteAsync(proxyServer.Id);

        // Assert
        result.IsDeleted.ShouldBeFalse();
        result.UsingCategoryDefaults.ShouldBe([
            ProxyCategory.HosterUploads,
            ProxyCategory.ImageHosters,
        ]);
        repository.ProxyServers.ShouldHaveSingleItem();
        repository.SaveChangesCallCount.ShouldBe(0);
        proxyRoutingCacheMock.Verify(
            cache => cache.RefreshAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Test]
    public async Task TestConnectionAsync_StoredCredentials_PassesDecryptedPasswordToTester()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(encryptedPassword: StoredEncryptedPassword);
        var expectedResult = new ProxyServerConnectionTestResult(
            ProxyServerConnectionTestOutcome.Success,
            TechnicalDetail: null
        );
        connectionTesterMock
            .Setup(tester =>
                tester.TestAsync(
                    new ProxyServerConnectionTestRequest(
                        ProxyType.Http,
                        "proxy.example.com",
                        8080,
                        "alice",
                        "stored"
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResult);

        // Act
        var result = await service.TestConnectionAsync(proxyServer.Id);

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Test]
    public async Task TestConnectionAsync_NoStoredPassword_PassesNullPasswordToTester()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();
        connectionTesterMock
            .Setup(tester =>
                tester.TestAsync(
                    It.IsAny<ProxyServerConnectionTestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ProxyServerConnectionTestResult(
                    ProxyServerConnectionTestOutcome.AuthenticationRequired,
                    "HTTP/1.1 407 Proxy Authentication Required"
                )
            );

        // Act
        var result = await service.TestConnectionAsync(proxyServer.Id);

        // Assert
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.AuthenticationRequired);
        connectionTesterMock.Verify(
            tester =>
                tester.TestAsync(
                    It.Is<ProxyServerConnectionTestRequest>(request => request.Password == null),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task TestConnectionAsync_UnreadableSecrets_ReturnsUnreadablePasswordWithoutTesting()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer(
            encryptedPassword: StoredEncryptedPassword,
            hasUnreadableSecrets: true
        );

        // Act
        var result = await service.TestConnectionAsync(proxyServer.Id);

        // Assert
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.UnreadablePassword);
        connectionTesterMock.Verify(
            tester =>
                tester.TestAsync(
                    It.IsAny<ProxyServerConnectionTestRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Test]
    public async Task TestConnectionAsync_TesterTimesOut_ReturnsTimedOut()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();
        connectionTesterMock
            .Setup(tester =>
                tester.TestAsync(
                    It.IsAny<ProxyServerConnectionTestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (ProxyServerConnectionTestRequest _, CancellationToken cancellationToken) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new ProxyServerConnectionTestResult(
                        ProxyServerConnectionTestOutcome.Success,
                        TechnicalDetail: null
                    );
                }
            );

        // Act
        var result = await service.TestConnectionAsync(proxyServer.Id);

        // Assert
        result.Outcome.ShouldBe(ProxyServerConnectionTestOutcome.TimedOut);
    }

    [Test]
    public async Task TestConnectionAsync_CallerCancels_ThrowsOperationCanceledException()
    {
        // Arrange
        var proxyServer = AddStoredProxyServer();
        using var cancellationTokenSource = new CancellationTokenSource();
        connectionTesterMock
            .Setup(tester =>
                tester.TestAsync(
                    It.IsAny<ProxyServerConnectionTestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(async () =>
            {
                await cancellationTokenSource.CancelAsync();
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return new ProxyServerConnectionTestResult(
                    ProxyServerConnectionTestOutcome.Success,
                    TechnicalDetail: null
                );
            });

        // Act
        var act = () => service.TestConnectionAsync(proxyServer.Id, cancellationTokenSource.Token);

        // Assert
        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    private ProxyServer AddStoredProxyServer(
        string name = "Upload proxy",
        int port = 8080,
        string? encryptedPassword = null,
        bool hasUnreadableSecrets = false
    )
    {
        var proxyServer = new ProxyServer
        {
            Name = name,
            ProxyType = ProxyType.Http,
            Host = "proxy.example.com",
            Port = port,
            Username = "alice",
            EncryptedPassword = encryptedPassword,
            HasUnreadableSecrets = hasUnreadableSecrets,
        };
        repository.Add(proxyServer);

        return proxyServer;
    }

    private static ProxyServerInput CreateInput(
        string name = "Upload proxy",
        ProxyType proxyType = ProxyType.Http,
        string host = "proxy.example.com",
        int port = 8080,
        string? username = "alice",
        string? password = null,
        bool removeStoredPassword = false
    )
    {
        return new ProxyServerInput(
            name,
            proxyType,
            host,
            port,
            username,
            password,
            removeStoredPassword
        );
    }
}
