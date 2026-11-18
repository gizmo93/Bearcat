using Bearcat.Domain.UseCases.RunUploadCompletionActions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.BackgroundTasks;

public class UploadCompletionActionBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<UploadCompletionActionBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Upload completion actions";

    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(5);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var runner = serviceProvider.GetRequiredService<UploadCompletionActionRunner>();
        await runner.RunAsync(stoppingToken);
    }
}
