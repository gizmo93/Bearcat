using Microsoft.Extensions.Configuration;
using Shouldly;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Domain.UnitTest.Shared;

public class TimeProviderTest
{
    [Test]
    public void ConvertToLocalTime_ConfiguredTimeZone_ConvertsToConfiguredTimeZone()
    {
        // Arrange
        var timeProvider = CreateTimeProvider("Europe/Berlin");
        var utcTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var localTime = timeProvider.ConvertToLocalTime(utcTime);

        // Assert
        localTime.ShouldBe(utcTime);
        localTime.Offset.ShouldBe(TimeSpan.FromHours(1));
        localTime.Hour.ShouldBe(13);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void ConvertToLocalTime_NoConfiguredTimeZone_ConvertsToSystemTimeZone(
        string? configuredTimeZoneId
    )
    {
        // Arrange
        var timeProvider = CreateTimeProvider(configuredTimeZoneId);
        var utcTime = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        var localTime = timeProvider.ConvertToLocalTime(utcTime);

        // Assert
        localTime.Offset.ShouldBe(TimeZoneInfo.Local.GetUtcOffset(utcTime));
    }

    private static TimeProvider CreateTimeProvider(string? configuredTimeZoneId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["LocalTimezone"] = configuredTimeZoneId }
            )
            .Build();

        return new TimeProvider(configuration);
    }
}
