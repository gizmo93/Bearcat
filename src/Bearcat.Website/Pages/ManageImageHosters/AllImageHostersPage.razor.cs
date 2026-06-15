using Bearcat.Abstractions.ImageHoster;
using Bearcat.Domain.UseCases.ManageImageHosters;
using Bearcat.Domain.UseCases.ManageImageHosters.ReadModels;
using Bearcat.Domain.UseCases.ManageImageHosters.Repositories;
using BlazorBlueprint.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bearcat.Website.Pages.ManageImageHosters;

public partial class AllImageHostersPage(
    IImageHosterRegistrationReadRepository readRepository,
    DialogService dialogService,
    ToastService toastService
)
{
    private IReadOnlyList<ImageHosterRegistrationReadModel> imageHosters = [];
    private ImageHosterService imageHosterService = null!;
    private HashSet<string> loginCapableClassNames = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadImageHostersAsync();
        imageHosterService = ScopedServices.GetRequiredService<ImageHosterService>();

        loginCapableClassNames = ScopedServices
            .GetRequiredService<IImageHosterFactory>()
            .GetImageHosters()
            .Where(imageHoster => imageHoster.SupportsLogin)
            .Select(imageHoster => imageHoster.ClassName)
            .ToHashSet();
    }

    private bool SupportsLogin(ImageHosterRegistrationReadModel imageHoster)
    {
        return loginCapableClassNames.Contains(imageHoster.ImageHosterClassName);
    }

    private async Task LoadImageHostersAsync()
    {
        imageHosters = await readRepository.GetAllAsync();
    }

    private async Task ShowAddDialogAsync()
    {
        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            new DialogOpenOptions
            {
                Title = L["AddImageHoster"],
                Description = L["ImageHosterDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadImageHostersAsync();
        }
    }

    private async Task ShowEditDialogAsync(ImageHosterRegistrationReadModel imageHoster)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditDialog.ImageHosterRegistrationId)] =
                imageHoster.ImageHosterRegistrationId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", imageHoster.Name],
                Description = L["ImageHosterDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadImageHostersAsync();
        }
    }

    private async Task ToggleIsActiveAsync(ImageHosterRegistrationReadModel imageHoster)
    {
        await imageHosterService.ToggleIsActiveAsync(imageHoster.ImageHosterRegistrationId);

        toastService.Success(
            imageHoster.IsActive
                ? L["ImageHosterRegistrationDeactivated", imageHoster.Name]
                : L["ImageHosterRegistrationActivated", imageHoster.Name]
        );
        await LoadImageHostersAsync();
    }

    private async Task DeleteAsync(ImageHosterRegistrationReadModel imageHoster)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", imageHoster.Name],
            L["DeleteImageHosterRegistrationConfirmation", imageHoster.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!result.Confirmed)
        {
            return;
        }

        await imageHosterService.DeleteAsync(imageHoster.ImageHosterRegistrationId);
        await LoadImageHostersAsync();
    }

    private async Task TryLoginAsync(ImageHosterRegistrationReadModel imageHoster)
    {
        await using var scope = ScopedServices.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ImageHosterService>();

        var result = await service.TryLoginAsync(imageHoster.ImageHosterRegistrationId);

        if (result.IsSuccess)
        {
            toastService.Success(L["LoginSuccessful", imageHoster.Name]);
            await LoadImageHostersAsync();
            return;
        }

        toastService.Error(L["LoginFailed", imageHoster.Name, result.ErrorMessage ?? string.Empty]);
        await LoadImageHostersAsync();
    }
}
