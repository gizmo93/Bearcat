using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationServiceTest : BearcatIntegrationTest
{
    private const string DistributionSiteClassName = "TestForum";
    private const string StoredSerializedConfig = "stored-config";

    private BearcatDbContext dbContext = null!;
    private Mock<IDistributionSite> distributionSiteMock = null!;
    private Mock<IDistributionSiteFactory> distributionSiteFactoryMock = null!;
    private DistributionSiteRegistrationService registrationService = null!;

    [SetUp]
    public void Setup()
    {
        dbContext = Database.CreateDbContext();
        distributionSiteMock = new Mock<IDistributionSite>(MockBehavior.Strict);
        distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        distributionSiteFactoryMock
            .Setup(factory => factory.Get(DistributionSiteClassName))
            .Returns(distributionSiteMock.Object);

        registrationService = new DistributionSiteRegistrationService(
            new DistributionSiteRegistrationWriteRepository(dbContext),
            new DistributionSiteRegistrationReadRepository(
                dbContext,
                distributionSiteFactoryMock.Object
            ),
            distributionSiteFactoryMock.Object,
            NoOpSecretProtector.Instance,
            UnreadableSecretsNotificationServiceFactory.Create(
                dbContext,
                CreateNotificationConfigurationProvider()
            )
        );
    }

    [TearDown]
    public async Task DisposeDbContextAsync()
    {
        await dbContext.DisposeAsync();
    }

    [Test]
    public async Task CreateAsync_ValidValues_StoresNormalizedConfig()
    {
        // Arrange
        SetupConfigurationFields();

        // Act
        await registrationService.CreateAsync(
            "Forum",
            DistributionSiteClassName,
            new Dictionary<string, object?>
            {
                ["BaseUrl"] = " https://example.org/community/ ",
                ["Username"] = " uploader ",
                ["Password"] = "secret",
            },
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.DistributionSiteRegistrations.SingleAsync();

        result.Name.ShouldBe("Forum");
        result.DistributionSiteClassName.ShouldBe(DistributionSiteClassName);
        result.SerializedConfig.ShouldBe(
            "{\"BaseUrl\":\"https://example.org/community/\",\"Username\":\"uploader\",\"Password\":\"secret\"}"
        );
        result.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task CreateAsync_InvalidUrl_ThrowsAndStoresNothing()
    {
        // Arrange
        SetupConfigurationFields();

        // Act
        var exception = await Should.ThrowAsync<ConfigurationFieldValidationException>(() =>
            registrationService.CreateAsync(
                "Forum",
                DistributionSiteClassName,
                new Dictionary<string, object?>
                {
                    ["BaseUrl"] = "example.org",
                    ["Username"] = "uploader",
                    ["Password"] = "secret",
                },
                CancellationToken.None
            )
        );

        // Assert
        exception.FieldKey.ShouldBe("BaseUrl");
        exception.Error.ShouldBe(ConfigurationFieldValidationError.InvalidUrl);
        (await dbContext.DistributionSiteRegistrations.AnyAsync()).ShouldBeFalse();
    }

    [Test]
    public async Task UpdateAsync_RegistrationHasUnreadableSecrets_ReplacesConfigAndClearsFlag()
    {
        // Arrange
        SetupConfigurationFields();
        var registration = await AddRegistrationWithUnreadableSecretsAsync();

        // Act
        await registrationService.UpdateAsync(
            registration.Id,
            "Forum",
            new Dictionary<string, object?>
            {
                ["BaseUrl"] = "https://example.org/",
                ["Username"] = "uploader",
                ["Password"] = "new-password",
            },
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.DistributionSiteRegistrations.SingleAsync();

        result.SerializedConfig.ShouldBe(
            "{\"BaseUrl\":\"https://example.org/\",\"Username\":\"uploader\",\"Password\":\"new-password\"}"
        );
        result.HasUnreadableSecrets.ShouldBeFalse();
        distributionSiteMock.Verify(
            site => site.DeserializeConfig(It.IsAny<string>()),
            Times.Never
        );
    }

    [Test]
    public async Task UpdateAsync_EmptyPassword_KeepsStoredPasswordAndClearsSession()
    {
        // Arrange
        SetupConfigurationFields();
        SetupStoredConfig();
        var registration = await AddReadableRegistrationAsync();

        // Act
        await registrationService.UpdateAsync(
            registration.Id,
            "Renamed forum",
            new Dictionary<string, object?>
            {
                ["BaseUrl"] = "https://example.org/forum/",
                ["Username"] = "new-uploader",
                ["Password"] = "",
            },
            CancellationToken.None
        );

        // Assert
        dbContext.ChangeTracker.Clear();
        var result = await dbContext.DistributionSiteRegistrations.SingleAsync();

        result.Name.ShouldBe("Renamed forum");
        result.SerializedConfig.ShouldBe(
            "{\"BaseUrl\":\"https://example.org/forum/\",\"Username\":\"new-uploader\",\"Password\":\"stored-password\"}"
        );
        result.EncryptedSession.ShouldBeNull();
    }

    [Test]
    public async Task GetConfigValuesWithoutSecretsAsync_StoredConfig_ReturnsValuesWithoutPassword()
    {
        // Arrange
        SetupConfigurationFields();
        SetupStoredConfig();
        var registration = await AddReadableRegistrationAsync();

        // Act
        var result = await registrationService.GetConfigValuesWithoutSecretsAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new Dictionary<string, object?>
            {
                ["BaseUrl"] = "https://example.org/community/",
                ["Username"] = "uploader",
            }
        );
    }

    [Test]
    public async Task GetBaseUrlAsync_ReadableConfig_ReturnsBaseUrlOfSite()
    {
        // Arrange
        var storedConfig = SetupStoredConfig();
        distributionSiteMock
            .Setup(site => site.GetBaseUrl(storedConfig))
            .Returns("https://example.org/community/");
        var registration = await AddReadableRegistrationAsync();

        // Act
        var result = await registrationService.GetBaseUrlAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe("https://example.org/community/");
    }

    [Test]
    public async Task GetBaseUrlAsync_RegistrationHasUnreadableSecrets_ReturnsNull()
    {
        // Arrange
        var registration = await AddRegistrationWithUnreadableSecretsAsync();

        // Act
        var result = await registrationService.GetBaseUrlAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetForumPostingRuleCountAsync_RulesOfSeveralRegistrations_CountsOnlyRulesOfRegistration()
    {
        // Arrange
        var registration = CreateRegistration("Primary forum");
        var otherRegistration = CreateRegistration("Other forum");
        var forumPostTemplate = new ForumPostTemplate
        {
            Name = "Release post",
            TemplateBody = string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        dbContext.ForumPostingRules.AddRange(
            CreateRule(registration, forumPostTemplate, sortOrder: 0),
            CreateRule(registration, forumPostTemplate, sortOrder: 1),
            CreateRule(otherRegistration, forumPostTemplate, sortOrder: 0)
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await registrationService.GetForumPostingRuleCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(2);
    }

    [Test]
    public async Task GetForumPostingRuleCountAsync_RegistrationWithoutRules_ReturnsZero()
    {
        // Arrange
        var registration = CreateRegistration("Primary forum");
        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await registrationService.GetForumPostingRuleCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
    }

    private void SetupConfigurationFields()
    {
        distributionSiteMock
            .Setup(site => site.ConfigurationFields)
            .Returns([
                new ConfigurationField("BaseUrl", ConfigurationFieldType.Url, IsRequired: true),
                new ConfigurationField("Username", ConfigurationFieldType.Text, IsRequired: true),
                new ConfigurationField(
                    "Password",
                    ConfigurationFieldType.Password,
                    IsRequired: true
                ),
            ]);
    }

    private IDistributionSiteConfig SetupStoredConfig()
    {
        var storedConfig = new Mock<IDistributionSiteConfig>(MockBehavior.Strict);
        storedConfig
            .Setup(config => config.ToDictionary())
            .Returns(
                new Dictionary<string, object?>
                {
                    ["BaseUrl"] = "https://example.org/community/",
                    ["Username"] = "uploader",
                    ["Password"] = "stored-password",
                }
            );
        distributionSiteMock
            .Setup(site => site.DeserializeConfig(StoredSerializedConfig))
            .Returns(storedConfig.Object);

        return storedConfig.Object;
    }

    private async Task<DistributionSiteRegistration> AddReadableRegistrationAsync()
    {
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum",
            DistributionSiteClassName = DistributionSiteClassName,
            SerializedConfig = StoredSerializedConfig,
            EncryptedSession = "stored-session",
            IsActive = true,
        };

        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return registration;
    }

    private async Task<DistributionSiteRegistration> AddRegistrationWithUnreadableSecretsAsync()
    {
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum",
            DistributionSiteClassName = DistributionSiteClassName,
            SerializedConfig = "unreadable",
            IsActive = true,
            HasUnreadableSecrets = true,
        };

        dbContext.DistributionSiteRegistrations.Add(registration);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return registration;
    }

    private static DistributionSiteRegistration CreateRegistration(string name)
    {
        return new DistributionSiteRegistration
        {
            Name = name,
            DistributionSiteClassName = "TestDistributionSite",
            SerializedConfig = "{}",
            IsActive = true,
        };
    }

    private static ForumPostingRule CreateRule(
        DistributionSiteRegistration registration,
        ForumPostTemplate forumPostTemplate,
        int sortOrder
    )
    {
        return new ForumPostingRule
        {
            DistributionSiteRegistration = registration,
            SortOrder = sortOrder,
            Name = $"Rule {sortOrder}",
            ConditionJson = "{}",
            TargetNodeId = "1",
            TargetPathSnapshot = "Forum",
            ForumPostTemplate = forumPostTemplate,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
