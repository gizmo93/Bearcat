using Bearcat.Abstractions.Configurations;
using Bearcat.Domain.Configurations;
using Bearcat.Domain.UseCases.ManageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;

namespace Bearcat.Domain.UseCases.ManageUploads;

public class RunningUploadReadService(
    IUploadReadRepository uploadReadRepository,
    IApplicationConfigurationProvider configurationProvider
)
{
    public async Task<IReadOnlyList<RunningUploadReadModel>> GetRunningUploadsInStartOrderAsync(
        CancellationToken cancellationToken = default
    )
    {
        var runningUploads = await uploadReadRepository.GetRunningUploadsAsync(cancellationToken);

        var uploadStartOrder = configurationProvider.GetValue<
            UploadConcurrencyConfiguration,
            UploadStartOrder
        >(c => c.UploadStartOrder);

        var uploadsInStartOrder = runningUploads.OrderByDescending(upload => upload.PrioritizedAt);

        if (uploadStartOrder == UploadStartOrder.InitialUploadsBeforeReuploads)
        {
            uploadsInStartOrder = uploadsInStartOrder.ThenBy(upload => upload.IsReupload);
        }

        return uploadsInStartOrder.ThenBy(upload => upload.UploadId).ToList();
    }
}
