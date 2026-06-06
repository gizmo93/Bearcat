using Bearcat.Domain.UseCases.ManageImageUploads.ReadModels;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageImageUploads;

public partial class ImageUploadUrlsDialog(IServiceScopeFactory serviceScopeFactory)
{
    [Parameter]
    public int ReleaseId { get; set; }

    [Parameter]
    public int ImageUploadId { get; set; }

    private IReadOnlyList<ReleaseImageUploadUrlReadModel> urls = [];

    protected override async Task OnInitializedAsync()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var readRepository = scope.ServiceProvider.GetRequiredService<IReleaseReadRepository>();
        urls = await readRepository.GetImageUploadUrlsAsync(ReleaseId, ImageUploadId);
    }
}
