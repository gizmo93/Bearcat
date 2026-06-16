namespace Bearcat.Abstractions.Updates;

public interface IUpdateChecker
{
    string CurrentVersion { get; }

    Task<UpdateStatus> GetUpdateStatusAsync(CancellationToken cancellationToken = default);
}
