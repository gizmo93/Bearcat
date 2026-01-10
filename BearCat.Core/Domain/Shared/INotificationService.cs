namespace BearCat.Core.Domain.Shared;

public interface INotificationService
{
    Task CreateInfoAsync(string message, CancellationToken cancellationToken);
    Task CreateWarningAsync(string message, CancellationToken cancellationToken);
    Task CreateErrorAsync(string message, CancellationToken cancellationToken);
}
