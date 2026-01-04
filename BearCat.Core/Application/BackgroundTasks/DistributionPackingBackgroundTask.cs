using BearCat.Core.Domain.UseCases.ManageDistributions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BearCat.Core.Application.BackgroundTasks;

public class DistributionPackingBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DistributionPackingBackgroundTask> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Distribution Packing Background Task");
        
        while (true)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var packingService = scope.ServiceProvider.GetRequiredService<DistributionPackingBackgroundService>();
            await packingService.PackPendingDistributionsAsync(stoppingToken);
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
