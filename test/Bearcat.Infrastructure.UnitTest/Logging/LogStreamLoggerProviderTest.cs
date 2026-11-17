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
}
