using Bearcat.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Logging;

public class LogStreamLoggerProviderTest
{
    private LogStreamBroadcaster broadcaster = null!;
    private LogStreamLoggerProvider provider = null!;

    [SetUp]
    public void SetUp()
    {
        broadcaster = new LogStreamBroadcaster();
        provider = new LogStreamLoggerProvider(broadcaster);
    }

    [TearDown]
    public void TearDown()
    {
        provider.Dispose();
    }

    [Test]
    public void Log_PublishesFormattedMessageWithCategoryAndLevel()
    {
        // Arrange
        var logger = provider.CreateLogger("Bearcat.Domain.SomeService");

        // Act
        logger.LogWarning("Upload {UploadId} is slow", 42);

        // Assert
        var line = broadcaster.GetSnapshot().Single();
        line.Category.ShouldBe("Bearcat.Domain.SomeService");
        line.Level.ShouldBe(LogLevel.Warning);
        line.Message.ShouldBe("Upload 42 is slow");
        line.Exception.ShouldBeNull();
    }

    [Test]
    public void Log_WithException_RendersExceptionAsText()
    {
        // Arrange
        var logger = provider.CreateLogger("Bearcat.Infrastructure.Test");
        var exception = new InvalidOperationException("boom");

        // Act
        logger.LogError(exception, "Upload failed");

        // Assert
        var line = broadcaster.GetSnapshot().Single();
        line.Level.ShouldBe(LogLevel.Error);
        line.Message.ShouldBe("Upload failed");
        line.Exception.ShouldNotBeNull();
        line.Exception.ShouldContain(nameof(InvalidOperationException));
        line.Exception.ShouldContain("boom");
    }

    [Test]
    public void Log_ReachesActiveSubscriber()
    {
        // Arrange
        using var subscription = broadcaster.Subscribe();
        var logger = provider.CreateLogger("Bearcat.Test");

        // Act
        logger.LogInformation("hello");

        // Assert
        subscription.Reader.TryRead(out var line).ShouldBeTrue();
        line!.Message.ShouldBe("hello");
    }

    [Test]
    [TestCase(LogLevel.Trace, true)]
    [TestCase(LogLevel.Debug, true)]
    [TestCase(LogLevel.Information, true)]
    [TestCase(LogLevel.Warning, true)]
    [TestCase(LogLevel.Error, true)]
    [TestCase(LogLevel.Critical, true)]
    [TestCase(LogLevel.None, false)]
    public void IsEnabled_EnabledForEveryLevelButNone(LogLevel logLevel, bool expected)
    {
        // Arrange
        var logger = provider.CreateLogger("Bearcat.Test");

        // Act / Assert
        logger.IsEnabled(logLevel).ShouldBe(expected);
    }

    [Test]
    public void BeginScope_ReturnsDisposableThatDoesNotPublish()
    {
        // Arrange
        var logger = provider.CreateLogger("Bearcat.Test");

        // Act
        using (logger.BeginScope("scope"))
        {
            // Assert
            broadcaster.GetSnapshot().ShouldBeEmpty();
        }
    }

    [Test]
    public void CreateLogger_KeepsPublishingIntoRingBufferWithoutSubscribers()
    {
        // Arrange
        var logger = provider.CreateLogger("Bearcat.Test");

        // Act
        logger.LogInformation("first");
        logger.LogInformation("second");

        // Assert
        broadcaster.GetSnapshot().Select(line => line.Message).ShouldBe(["first", "second"]);
    }
}
