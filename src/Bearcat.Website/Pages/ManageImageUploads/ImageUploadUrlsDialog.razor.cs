using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageImageUploads;

public partial class ImageUploadUrlsDialog(IScopedOperationRunner operationRunner)
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int ImageUploadId { get; set; }

    private IReadOnlyList<ReleaseImageUploadUrlReadModel> urls = [];

    protected override async Task OnInitializedAsync()
    {
        urls = await operationRunner.RunAsync(
            (IReleaseReadRepository repository) =>
                repository.GetImageUploadUrlsAsync(ReleaseId, ImageUploadId)
        );
    }
}
