using System.Threading.Channels;

namespace Bearcat.Infrastructure.Logging;

public sealed class LogStreamBroadcaster
{
    public const int BufferCapacity = 500;
    public const int SubscriberCapacity = 2000;

    private readonly LogLine[] buffer = new LogLine[BufferCapacity];
    private readonly Lock bufferLock = new();
    private readonly Lock subscriberLock = new();

    private volatile LogStreamSubscription[] subscribers = [];
    private int nextIndex;
    private int count;

    public void Publish(LogLine line)
    {
        lock (bufferLock)
        {
            buffer[nextIndex] = line;
            nextIndex = (nextIndex + 1) % BufferCapacity;

            if (count < BufferCapacity)
            {
                count++;
            }
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.Publish(line);
        }
    }

    public IReadOnlyList<LogLine> GetSnapshot()
    {
        lock (bufferLock)
        {
            var snapshot = new List<LogLine>(count);
            var oldestIndex = ((nextIndex - count) + BufferCapacity) % BufferCapacity;

            for (var offset = 0; offset < count; offset++)
            {
                snapshot.Add(buffer[(oldestIndex + offset) % BufferCapacity]);
            }

            return snapshot;
        }
    }

    public LogStreamSubscription Subscribe()
    {
        var channel = Channel.CreateBounded<LogLine>(
            new BoundedChannelOptions(SubscriberCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            }
        );

        var subscription = new LogStreamSubscription(this, channel);

        lock (subscriberLock)
        {
            subscribers = [.. subscribers, subscription];
        }

        return subscription;
    }

    internal void Unsubscribe(LogStreamSubscription subscription)
    {
        lock (subscriberLock)
        {
            subscribers = [.. subscribers.Where(existing => existing != subscription)];
        }
    }
}
