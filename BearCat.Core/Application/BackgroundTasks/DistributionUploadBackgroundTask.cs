using BearCat.Core.Domain.UseCases.ManageDistributions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Application.BackgroundTasks;

public class DistributionUploadBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DistributionUploadBackgroundTask> logger) : BackgroundService
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
            var uploadService = scope.ServiceProvider.GetRequiredService<DistributionUploadService>();
            await uploadService.UploadPendingDistributionsAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
