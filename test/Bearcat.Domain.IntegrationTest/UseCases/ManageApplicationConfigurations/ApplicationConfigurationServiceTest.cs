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

public class ApplicationConfigurationServiceTest : BearcatIntegrationTest
{
    private const string ConfigurationKey = "ArchiveCleanup";
    private const string PropertyName = "AutoCleanup";

    private Mock<IApplicationConfigurationOverrideCache> overrideCacheMock = null!;
    private BearcatDbContext readDbContext = null!;
    private ApplicationConfigurationService service = null!;
    private BearcatDbContext writeDbContext = null!;

    [SetUp]
    public void Setup()
    {
        readDbContext = Database.CreateDbContext();
        readDbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        writeDbContext = Database.CreateDbContext();

        var repository = new ApplicationConfigurationOverrideRepository(
            readDbContext,
            writeDbContext
        );
        overrideCacheMock = new Mock<IApplicationConfigurationOverrideCache>(MockBehavior.Strict);

        service = new ApplicationConfigurationService(
            CreateRegistry(),
            repository,
            repository,
            overrideCacheMock.Object,
            CreateTimeProvider()
        );
    }

    [TearDown]
    public async Task DisposeServiceDbContextsAsync()
    {
        await readDbContext.DisposeAsync();
        await writeDbContext.DisposeAsync();
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

        var property = configuration.Properties.Single();
        property.ConfigurationKey.ShouldBe(ConfigurationKey);
        property.Name.ShouldBe(PropertyName);
        property.DisplayName.ShouldBe("AutoCleanup");
        property.Description.ShouldBe("AutoCleanupDescription");
        property.ValueType.ShouldBe(typeof(bool));
        property.DefaultValue.ShouldBe(false);
        property.CurrentValue.ShouldBe(false);
        property.IsOverridden.ShouldBeFalse();
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

        var property = result.Single().Properties.Single();
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
        var result = await writeDbContext.ApplicationConfigurationOverrides.SingleAsync();

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
        var result = await writeDbContext.ApplicationConfigurationOverrides.SingleAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(configurationOverride.Id);
        result.SerializedValue.ShouldBe("true");
        result.UpdatedAt.ShouldBeGreaterThan(originalUpdatedAt);
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
        var result = await writeDbContext.ApplicationConfigurationOverrides.AnyAsync();

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
        var result = await writeDbContext.ApplicationConfigurationOverrides.AnyAsync();

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

        writeDbContext.ApplicationConfigurationOverrides.Add(configurationOverride);
        await writeDbContext.SaveChangesAsync();

        return configurationOverride;
    }
}
