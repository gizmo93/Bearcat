using Bearcat.Domain.UseCases.ManageUploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bearcat.Application.BackgroundTasks;

public class ArchiveUploadBackgroundTask(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ArchiveUploadBackgroundTask> logger
) : AbstractBackgroundTask(serviceScopeFactory, logger)
{
    protected override string DisplayName => "Archive upload";

    protected override TimeSpan Interval => TimeSpan.FromSeconds(20);

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken stoppingToken
    )
    {
        var uploadService = serviceProvider.GetRequiredService<UploadFilesService>();
        await uploadService.ProcessAsync(stoppingToken);
    }
}
