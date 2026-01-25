using BearCat.Core.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Application.BackgroundTasks;

public class ArchiveUploadBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveUploadBackgroundTask> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Distribution Upload Background Task");

        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var uploadService = scope.ServiceProvider.GetRequiredService<UploadFilesService>();
            await uploadService.ProcessPendingUploadsAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
