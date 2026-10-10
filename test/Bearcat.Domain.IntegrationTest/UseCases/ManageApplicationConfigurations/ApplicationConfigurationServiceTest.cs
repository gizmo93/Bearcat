using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.Entities;
using Bearcat.Domain.UseCases.ManageApplicationConfigurations;
using Bearcat.Infrastructure.Database;
using Bearcat.Infrastructure.Database.Repositories;
using Bearcat.IntegrationTest.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.IntegrationTest.UseCases.ManageApplicationConfigurations;

public class ApplicationConfigurationServiceTest(DatabaseProvider databaseProvider)
    : BearcatIntegrationTest(databaseProvider)
{
    private const string ConfigurationKey = "ArchiveCleanup";
    private const string PropertyName = "AutoConvertToUnmanaged";

    private Mock<IApplicationConfigurationOverrideCache> overrideCacheMock = null!;
    private ApplicationConfigurationService service = null!;

    [SetUp]
    public void Setup()
    {
        var repository = new ApplicationConfigurationOverrideRepository(ReadDbContext, DbContext);
        overrideCacheMock = new Mock<IApplicationConfigurationOverrideCache>(MockBehavior.Strict);

        service = new ApplicationConfigurationService(
            CreateRegistry(),
            repository,
            repository,
            overrideCacheMock.Object,
            CreateTimeProvider()
        );
    }

    [Test]
    public async Task GetAllAsync_NoOverrides_ReturnsDefaultApplicationConfigurations()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        // Act
        var result = await service.GetAllAsync(cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        var configuration = result.Single();
        configuration.DisplayName.ShouldBe("ArchiveCleanup");
        configuration.Description.ShouldBe("ArchiveCleanupDescription");

        var property = configuration.Properties.Single(p => p.Name == PropertyName);
        property.ConfigurationKey.ShouldBe(ConfigurationKey);
        property.DisplayName.ShouldBe("AutoConvertToUnmanaged");
        property.Description.ShouldBe("AutoConvertToUnmanagedDescription");
        property.ValueType.ShouldBe(typeof(bool));
        property.DefaultValue.ShouldBe(false);
        property.CurrentValue.ShouldBe(false);
        property.IsOverridden.ShouldBeFalse();
    }

    [Test]
    public async Task GetAllAsync_ConfigurationHasSeveralProperties_KeepsDeclarationOrder()
    {
        // Act
        var result = await service.GetAllAsync(CancellationToken.None);

        // Assert
        result
            .Single()
            .Properties.Select(p => p.Name)
            .ShouldBe([
                "AutoConvertToUnmanaged",
                "ReleaseFolderRetentionDays",
                "DeleteReleaseFolderOnConversion",
                "ArchiveRetentionAction",
                "ArchiveRetentionDays",
            ]);
    }

    [Test]
    public async Task GetAllAsync_OverrideExists_ReturnsOverriddenCurrentValue()
    {
        // Arrange
        await AddOverrideAsync("true");

        // Act
        var result = await service.GetAllAsync(CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();

        var property = result.Single().Properties.Single(p => p.Name == PropertyName);
        property.DefaultValue.ShouldBe(false);
        property.CurrentValue.ShouldBe(true);
        property.IsOverridden.ShouldBeTrue();
    }

    [Test]
    public async Task SaveOverrideAsync_OverrideDoesNotExist_PersistsOverrideAndUpdatesCache()
    {
        // Arrange
        overrideCacheMock
            .Setup(c => c.SetOverride(ConfigurationKey, PropertyName, "true"))
            .Verifiable();

        // Act
        await service.SaveOverrideAsync(
            ConfigurationKey,
            PropertyName,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.ApplicationConfigurationOverrides.SingleAsync();

        result.ShouldNotBeNull();
        result.ConfigurationKey.ShouldBe(ConfigurationKey);
        result.PropertyName.ShouldBe(PropertyName);
        result.SerializedValue.ShouldBe("true");
        result.UpdatedAt.ShouldBeGreaterThan(DateTime.MinValue);
        overrideCacheMock.Verify();
    }

    [Test]
    public async Task SaveOverrideAsync_OverrideExists_UpdatesExistingOverrideAndUpdatesCache()
    {
        // Arrange
        var configurationOverride = await AddOverrideAsync("false");
        var originalUpdatedAt = configurationOverride.UpdatedAt;
        overrideCacheMock
            .Setup(c => c.SetOverride(ConfigurationKey, PropertyName, "true"))
            .Verifiable();

        // Act
        await service.SaveOverrideAsync(
            ConfigurationKey,
            PropertyName,
            true,
            CancellationToken.None
        );

        // Assert
        var result = await DbContext.ApplicationConfigurationOverrides.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(configurationOverride.Id);
        result.SerializedValue.ShouldBe("true");
        result.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
        overrideCacheMock.Verify();
    }

    [Test]
    public async Task SaveOverrideAsync_EnumProperty_PersistsNumericValueAndReturnsEnumCurrentValue()
    {
        // Arrange
        const string enumPropertyName = nameof(ArchiveCleanupConfiguration.ArchiveRetentionAction);
        overrideCacheMock
            .Setup(c => c.SetOverride(ConfigurationKey, enumPropertyName, "3"))
            .Verifiable();

        // Act
        await service.SaveOverrideAsync(
            ConfigurationKey,
            enumPropertyName,
            ArchiveRetentionAction.MoveToStorageFolder,
            CancellationToken.None
        );
        var result = await service.GetAllAsync(CancellationToken.None);

        // Assert
        var configurationOverride = await DbContext.ApplicationConfigurationOverrides.SingleAsync();
        configurationOverride.SerializedValue.ShouldBe("3");
        var property = result.Single().Properties.Single(p => p.Name == enumPropertyName);
        property.ValueType.ShouldBe(typeof(ArchiveRetentionAction));
        property.DefaultValue.ShouldBe(ArchiveRetentionAction.Off);
        property.CurrentValue.ShouldBe(ArchiveRetentionAction.MoveToStorageFolder);
        property.Options.ShouldBe(["Off", "Delete", "MoveToStorageFolder"]);
        overrideCacheMock.Verify();
    }

    [Test]
    public async Task ResetOverrideAsync_OverrideExists_RemovesOverrideAndClearsCache()
    {
        // Arrange
        await AddOverrideAsync("true");
        overrideCacheMock.Setup(c => c.RemoveOverride(ConfigurationKey, PropertyName)).Verifiable();

        // Act
        await service.ResetOverrideAsync(ConfigurationKey, PropertyName, CancellationToken.None);

        // Assert
        var result = await DbContext.ApplicationConfigurationOverrides.AnyAsync();

        result.ShouldBeFalse();
        overrideCacheMock.Verify();
    }

    [Test]
    public async Task ResetOverrideAsync_OverrideDoesNotExist_ClearsCache()
    {
        // Arrange
        overrideCacheMock
            .Setup(c => c.RemoveOverride(ConfigurationKey, PropertyName))
            .Verifiable();

        // Act
        await service.ResetOverrideAsync(ConfigurationKey, PropertyName, CancellationToken.None);

        // Assert
        var result = await DbContext.ApplicationConfigurationOverrides.AnyAsync();

        result.ShouldBeFalse();
        overrideCacheMock.Verify();
    }

    [Test]
    public async Task SaveOverrideAsync_PropertyIsNotRegistered_ThrowsInvalidOperationException()
    {
        // Arrange
        var unknownPropertyName = "MissingProperty";

        // Act
        var result = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.SaveOverrideAsync(
                ConfigurationKey,
                unknownPropertyName,
                true,
                CancellationToken.None
            )
        );

        // Assert
        result.ShouldNotBeNull();
        result.Message.ShouldBe(
            $"Configuration property {ConfigurationKey}.{unknownPropertyName} is not registered."
        );
    }

    private static ApplicationConfigurationRegistry CreateRegistry()
    {
        return new ApplicationConfigurationRegistry([
            new ApplicationConfigurationRegistration(typeof(ArchiveCleanupConfiguration)),
        ]);
    }

    private static TimeProvider CreateTimeProvider()
    {
        var configurationSectionMock = new Mock<IConfigurationSection>();
        configurationSectionMock.SetupGet(s => s.Value).Returns("UTC");

        var configurationMock = new Mock<IConfiguration>();
        configurationMock
            .Setup(c => c.GetSection("LocalTimezone"))
            .Returns(configurationSectionMock.Object);

        return new TimeProvider(configurationMock.Object);
    }

    private async Task<ApplicationConfigurationOverride> AddOverrideAsync(string serializedValue)
    {
        var configurationOverride = new ApplicationConfigurationOverride
        {
            ConfigurationKey = ConfigurationKey,
            PropertyName = PropertyName,
            SerializedValue = serializedValue,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1),
        };

        DbContext.ApplicationConfigurationOverrides.Add(configurationOverride);
        await DbContext.SaveChangesAsync();

        return configurationOverride;
    }
}
