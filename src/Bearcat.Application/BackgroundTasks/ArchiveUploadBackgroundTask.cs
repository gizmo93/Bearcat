using Bearcat.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchiveUploadBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveUploadBackgroundTask> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Distribution Upload Background Task");
        await Task.Yield();

        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            try
            {
                var uploadService = scope.ServiceProvider.GetRequiredService<UploadFilesService>();
                await uploadService.ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error occurred while processing archive uploads");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
