using System.Globalization;
using Bearcat.Domain.UseCases.ManageImageUploadConfigs;
using Bearcat.Domain.UseCases.ManageReleaseCollections.ReadModels;
using Bearcat.Website.Formatting;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleaseCollections.Images;

public partial class ReleaseCollectionImageUploads(
    IScopedOperationRunner operationRunner,
    DialogService dialogService,
    TimeProvider timeProvider
) : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public int ReleaseCollectionId { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<CollectionImageUploadReadModel> ImageUploads { get; set; } = [];

    [Parameter]
    public EventCallback OnImageUploadsChanged { get; set; }

    private async Task ShowAddDialogAsync()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditCollectionImageUploadConfigDialog.ReleaseCollectionId)] =
                ReleaseCollectionId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditCollectionImageUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["AddImageUploadConfig"],
                Description = L["CollectionImageUploadsDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await OnImageUploadsChanged.InvokeAsync();
        }
    }

    private async Task ShowEditDialogAsync(CollectionImageUploadReadModel imageUpload)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditCollectionImageUploadConfigDialog.ReleaseCollectionId)] =
                ReleaseCollectionId,
            [nameof(CreateOrEditCollectionImageUploadConfigDialog.ImageUploadConfigId)] =
                imageUpload.ImageUploadConfigId,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditCollectionImageUploadConfigDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = L["EditNamedItem", imageUpload.Name],
                Description = L["CollectionImageUploadsDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await OnImageUploadsChanged.InvokeAsync();
        }
    }

    private async Task DeleteConfigAsync(CollectionImageUploadReadModel imageUpload)
    {
        var result = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", imageUpload.Name],
            L["DeleteImageUploadConfigConfirmation", imageUpload.Name],
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

        await operationRunner.RunAsync(
            (ImageUploadConfigService service) =>
                service.DeleteAsync(imageUpload.ImageUploadConfigId)
        );

        await OnImageUploadsChanged.InvokeAsync();
    }

    private static bool IsImageHosterNameShown(CollectionImageUploadReadModel imageUpload) =>
        !string.Equals(
            imageUpload.Name,
            imageUpload.ImageHosterRegistrationName,
            StringComparison.OrdinalIgnoreCase
        );

    private static string GetImageUrlsText(CollectionImageUploadReadModel imageUpload) =>
        string.Join(Environment.NewLine, imageUpload.ImageUrls.Select(imageUrl => imageUrl.Url));

    private string HumanizeTimestamp(DateTime value) => timeProvider.Humanize(value);

    private static string FormatTimestamp(DateTime value) =>
        value.ToString("g", CultureInfo.CurrentCulture);
}
