using Bearcat.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Bearcat.Infrastructure.UnitTest.Logging;

public class LogStreamBroadcasterTest
{
    private LogStreamBroadcaster broadcaster = null!;

    [SetUp]
    public void SetUp()
    {
        broadcaster = new LogStreamBroadcaster();
    }

    [Test]
    public void GetSnapshot_WithoutPublishedLines_ReturnsEmptyList()
    {
        // Act
        var snapshot = broadcaster.GetSnapshot();

        // Assert
        snapshot.ShouldBeEmpty();
    }

    [Test]
    public void GetSnapshot_BufferNotFull_ReturnsLinesInChronologicalOrder()
    {
        // Arrange
        broadcaster.Publish(CreateLine("first"));
        broadcaster.Publish(CreateLine("second"));
        broadcaster.Publish(CreateLine("third"));

        // Act
        var snapshot = broadcaster.GetSnapshot();

        // Assert
        snapshot.Select(line => line.Message).ShouldBe(["first", "second", "third"]);
    }

    [Test]
    public void GetSnapshot_MoreLinesThanCapacity_KeepsNewestInChronologicalOrder()
    {
        // Arrange
        var publishedCount = LogStreamBroadcaster.BufferCapacity + 25;

        for (var index = 0; index < publishedCount; index++)
        {
            broadcaster.Publish(CreateLine($"line-{index}"));
        }

        // Act
        var snapshot = broadcaster.GetSnapshot();

        // Assert
        snapshot.Count.ShouldBe(LogStreamBroadcaster.BufferCapacity);
        snapshot[0].Message.ShouldBe("line-25");
        snapshot[^1].Message.ShouldBe($"line-{publishedCount - 1}");
        snapshot
            .Select(line => line.Message)
            .ShouldBe(
                Enumerable
                    .Range(25, LogStreamBroadcaster.BufferCapacity)
                    .Select(index => $"line-{index}")
            );
    }

    [Test]
    public void Publish_WithActiveSubscriber_DeliversLine()
    {
        // Arrange
        using var subscription = broadcaster.Subscribe();

        // Act
        broadcaster.Publish(CreateLine("streamed"));

        // Assert
        subscription.Reader.TryRead(out var received).ShouldBeTrue();
        received!.Message.ShouldBe("streamed");
    }

    [Test]
    public void Publish_WithMultipleSubscribers_DeliversToEach()
    {
        // Arrange
        using var first = broadcaster.Subscribe();
        using var second = broadcaster.Subscribe();

        // Act
        broadcaster.Publish(CreateLine("fan-out"));

        // Assert
        first.Reader.TryRead(out var firstLine).ShouldBeTrue();
        second.Reader.TryRead(out var secondLine).ShouldBeTrue();
        firstLine!.Message.ShouldBe("fan-out");
        secondLine!.Message.ShouldBe("fan-out");
    }

    [Test]
    public void Publish_AfterSubscriptionDisposed_DoesNotDeliver()
    {
        // Arrange
        var subscription = broadcaster.Subscribe();
        subscription.Dispose();

        // Act
        broadcaster.Publish(CreateLine("after-dispose"));

        // Assert
        subscription.Reader.TryRead(out _).ShouldBeFalse();
        subscription.Reader.Completion.IsCompleted.ShouldBeTrue();
    }

    [Test]
    public void Publish_SubscriberChannelFull_DropsOldestLines()
    {
        // Arrange
        using var subscription = broadcaster.Subscribe();
        var publishedCount = LogStreamBroadcaster.SubscriberCapacity + 10;

        // Act
        for (var index = 0; index < publishedCount; index++)
        {
            broadcaster.Publish(CreateLine($"line-{index}"));
        }

        // Assert
        var received = new List<string>();

        while (subscription.Reader.TryRead(out var line))
        {
            received.Add(line.Message);
        }

        received.Count.ShouldBe(LogStreamBroadcaster.SubscriberCapacity);
        received[0].ShouldBe("line-10");
        received[^1].ShouldBe($"line-{publishedCount - 1}");
    }

    [Test]
    public void Publish_WithoutSubscribers_StillFillsRingBuffer()
    {
        // Act
        broadcaster.Publish(CreateLine("history"));

        // Assert
        broadcaster.GetSnapshot().Single().Message.ShouldBe("history");
    }

    private static LogLine CreateLine(string message)
    {
        return new LogLine(
            DateTimeOffset.UnixEpoch,
            LogLevel.Information,
            "Bearcat.Test",
            message,
            null
        );
    }
}
