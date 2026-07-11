namespace Bearcat.Abstractions.Notifications;

public interface ITelegramNotificationProcessor
{
    bool HasPendingPairing { get; }

    Task PollPairingAsync(CancellationToken cancellationToken);

    Task ProcessDeliveriesAsync(CancellationToken cancellationToken);
}
