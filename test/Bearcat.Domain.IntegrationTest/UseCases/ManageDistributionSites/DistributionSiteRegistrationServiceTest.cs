using Bearcat.Abstractions.ConfigurationFields;
using Bearcat.Abstractions.DistributionSite;
using Bearcat.Domain.Entities;
using Bearcat.Domain.IntegrationTest.Shared.UnreadableSecrets;
using Bearcat.Domain.Shared.ConfigurationFields;
using Bearcat.Domain.UseCases.ManageDistributionSites;
using Bearcat.Domain.ValueObjects;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.Infrastructure.Security;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageDistributionSites;

public class DistributionSiteRegistrationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string DistributionSiteClassName = "TestForum";
    private const string FixedUrlDistributionSiteClassName = "FixedUrlForum";
    private const string StoredSerializedConfig = "stored-config";

    private Mock<IDistributionSite> distributionSiteMock = null!;
    private Mock<IDistributionSite> fixedUrlDistributionSiteMock = null!;
    private Mock<IDistributionSiteFactory> distributionSiteFactoryMock = null!;
    private DistributionSiteRegistrationService registrationService = null!;

    [SetUp]
    public void Setup()
    {
        distributionSiteMock = new Mock<IDistributionSite>(MockBehavior.Strict);
        fixedUrlDistributionSiteMock = new Mock<IDistributionSite>(MockBehavior.Strict);
        distributionSiteFactoryMock = new Mock<IDistributionSiteFactory>(MockBehavior.Strict);
        distributionSiteFactoryMock
            .Setup(factory => factory.GetByClassName(DistributionSiteClassName))
            .Returns(distributionSiteMock.Object);
        distributionSiteFactoryMock
            .Setup(factory => factory.GetByClassName(FixedUrlDistributionSiteClassName))
            .Returns(fixedUrlDistributionSiteMock.Object);

        registrationService = new DistributionSiteRegistrationService(
            new DistributionSiteRegistrationWriteRepository(DbContext),
            new DistributionSiteRegistrationReadRepository(
                DbContext,
                distributionSiteFactoryMock.Object
            ),
            distributionSiteFactoryMock.Object,
            NoOpSecretProtector.Instance,
            UnreadableSecretsNotificationServiceFactory.Create(
                DbContext,
                CreateNotificationConfigurationProvider()
            )
        );
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
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.DistributionSiteRegistrations.SingleAsync();

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
        (await DbContext.DistributionSiteRegistrations.AnyAsync()).ShouldBeFalse();
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
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.DistributionSiteRegistrations.SingleAsync();

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
        DbContext.ChangeTracker.Clear();
        var result = await DbContext.DistributionSiteRegistrations.SingleAsync();

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
    public async Task GetBaseUrlsByRegistrationIdAsync_ConfiguredAndFixedUrlSites_ReturnsBaseUrlOfEachRegistration()
    {
        // Arrange
        var storedConfig = SetupStoredConfig();
        distributionSiteMock
            .Setup(site => site.GetBaseUrl(storedConfig))
            .Returns("https://example.org/community/");
        var fixedUrlConfig = Mock.Of<IDistributionSiteConfig>();
        fixedUrlDistributionSiteMock
            .Setup(site => site.DeserializeConfig(StoredSerializedConfig))
            .Returns(fixedUrlConfig);
        fixedUrlDistributionSiteMock
            .Setup(site => site.GetBaseUrl(fixedUrlConfig))
            .Returns("https://fixed.example/");
        var configuredUrlRegistration = await AddReadableRegistrationAsync();
        var fixedUrlRegistration = await AddReadableRegistrationAsync(
            FixedUrlDistributionSiteClassName
        );
        await AddReadableRegistrationAsync();

        // Act
        var result = await registrationService.GetBaseUrlsByRegistrationIdAsync(
            [configuredUrlRegistration.Id, fixedUrlRegistration.Id],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new Dictionary<int, string>
            {
                [configuredUrlRegistration.Id] = "https://example.org/community/",
                [fixedUrlRegistration.Id] = "https://fixed.example/",
            },
            ignoreOrder: true
        );
    }

    [Test]
    public async Task GetBaseUrlsByRegistrationIdAsync_RegistrationHasUnreadableSecrets_LeavesRegistrationOut()
    {
        // Arrange
        var storedConfig = SetupStoredConfig();
        distributionSiteMock
            .Setup(site => site.GetBaseUrl(storedConfig))
            .Returns("https://example.org/community/");
        var readableRegistration = await AddReadableRegistrationAsync();
        var unreadableRegistration = await AddRegistrationWithUnreadableSecretsAsync();

        // Act
        var result = await registrationService.GetBaseUrlsByRegistrationIdAsync(
            [readableRegistration.Id, unreadableRegistration.Id],
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(
            new Dictionary<int, string>
            {
                [readableRegistration.Id] = "https://example.org/community/",
            }
        );
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
        DbContext.ForumPostingRules.AddRange(
            CreateRule(registration, forumPostTemplate, sortOrder: 0),
            CreateRule(registration, forumPostTemplate, sortOrder: 1),
            CreateRule(otherRegistration, forumPostTemplate, sortOrder: 0)
        );
        await DbContext.SaveChangesAsync();

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
        DbContext.DistributionSiteRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await registrationService.GetForumPostingRuleCountAsync(
            registration.Id,
            CancellationToken.None
        );

        // Assert
        result.ShouldBe(0);
    }

    [Test]
    public async Task DeleteAsync_RegistrationHasRulesAndPostedLocations_DeletesRulesAndKeepsPostedLocationsWithoutRegistration()
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
        var otherRule = CreateRule(otherRegistration, forumPostTemplate, sortOrder: 0);
        DbContext.ForumPostingRules.AddRange(
            CreateRule(registration, forumPostTemplate, sortOrder: 0),
            CreateRule(registration, forumPostTemplate, sortOrder: 1),
            otherRule
        );
        var postedLocation = new PostedLocation
        {
            Release = new Release
            {
                Name = "Bearcat.Release.001",
                CreatedAt = DateTime.UtcNow,
                ReleaseType = ReleaseType.Managed,
                ReleaseFolderPath = "/tmp/Bearcat.Release.001",
                ReleaseGroup = new ReleaseGroup
                {
                    Name = "Release group",
                    EnableAutomaticReuploads = false,
                    NumberOfHoursUntilReupload = 24,
                },
            },
            DistributionSiteRegistration = registration,
            ForumPostTemplate = forumPostTemplate,
            Url = "https://example.org/threads/1",
            CreatedAt = DateTime.UtcNow,
        };
        DbContext.PostedLocations.Add(postedLocation);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        await registrationService.DeleteAsync(registration.Id, CancellationToken.None);

        // Assert
        DbContext.ChangeTracker.Clear();
        (
            await DbContext.DistributionSiteRegistrations.Select(site => site.Id).ToListAsync()
        ).ShouldBe([otherRegistration.Id]);
        (await DbContext.ForumPostingRules.Select(rule => rule.Id).ToListAsync()).ShouldBe([
            otherRule.Id,
        ]);
        var remainingPostedLocation = await DbContext.PostedLocations.SingleAsync();
        remainingPostedLocation.Id.ShouldBe(postedLocation.Id);
        remainingPostedLocation.DistributionSiteRegistrationId.ShouldBeNull();
        remainingPostedLocation.ForumPostTemplateId.ShouldBe(forumPostTemplate.Id);
        (await DbContext.ForumPostTemplates.CountAsync()).ShouldBe(1);
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

    private async Task<DistributionSiteRegistration> AddReadableRegistrationAsync(
        string distributionSiteClassName = DistributionSiteClassName
    )
    {
        var registration = new DistributionSiteRegistration
        {
            Name = "Forum",
            DistributionSiteClassName = distributionSiteClassName,
            SerializedConfig = StoredSerializedConfig,
            EncryptedSession = "stored-session",
            IsActive = true,
        };

        DbContext.DistributionSiteRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

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

        DbContext.DistributionSiteRegistrations.Add(registration);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

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
