using System.Threading.Channels;

namespace Bearcat.Infrastructure.Logging;

public sealed class LogStreamSubscription(
    LogStreamBroadcaster broadcaster,
    Channel<LogLine> channel
) : IDisposable
{
    public ChannelReader<LogLine> Reader => channel.Reader;

    public void Dispose()
    {
        broadcaster.Unsubscribe(this);
        channel.Writer.TryComplete();
    }

    internal void Publish(LogLine line)
    {
        channel.Writer.TryWrite(line);
    }
}
