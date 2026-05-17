using Bearcat.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Application.BackgroundTasks;

public class CheckUploadStateBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CheckUploadStateBackgroundTask> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Check Upload State Background Task");
        await Task.Yield();

        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var uploadStateService = scope.ServiceProvider.GetRequiredService<UploadStateService>();
            var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            await uploadStateService.CheckUploadStatesAsync(
                timeProvider.GetLocalNow(),
                stoppingToken
            );

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
