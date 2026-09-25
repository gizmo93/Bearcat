using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Deletion;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;

namespace Bearcat.Website.Pages.ManageAdditionalArchiveContents;

public partial class AdditionalArchiveContentsPage(
    DialogService dialogService,
    ToastService toastService,
    IScopedOperationRunner operationRunner
)
{
    private const int MaximumListedUsageNames = 5;

    private IReadOnlyList<AdditionalArchiveContentReadModel> contents = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadContentsAsync();
    }

    private async Task LoadContentsAsync()
    {
        contents = await operationRunner.RunAsync(
            (IAdditionalArchiveContentReadRepository repository) => repository.GetAllAsync()
        );
    }

    private async Task ShowAddDialogAsync()
    {
        await ShowDialogAsync(
            new AdditionalArchiveContentFormModel(),
            L["NewAdditionalArchiveContent"]
        );
    }

    private async Task ShowEditDialogAsync(AdditionalArchiveContentReadModel content)
    {
        var detail = await operationRunner.RunAsync(
            (IAdditionalArchiveContentReadRepository repository) =>
                repository.GetDetailReadModelAsync(content.Id)
        );

        if (detail is null)
        {
            return;
        }

        var formModel = new AdditionalArchiveContentFormModel
        {
            AdditionalArchiveContentId = detail.Id,
            Name = detail.Name,
            Type = detail.Type,
            SourcePath = detail.SourcePath,
            FileName = detail.FileName,
            TextContent = detail.TextContent,
        };

        await ShowDialogAsync(formModel, L["EditNamedItem", content.Name]);
    }

    private async Task ShowDialogAsync(AdditionalArchiveContentFormModel formModel, string title)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CreateOrEditAdditionalArchiveContentDialog.FormModel)] = formModel,
        };

        var dialog = await dialogService.OpenAsync<CreateOrEditAdditionalArchiveContentDialog>(
            parameters,
            new DialogOpenOptions
            {
                Title = title,
                Description = L["AdditionalArchiveContentDialogDescription"],
                Size = DialogSize.Large,
                ShowClose = true,
                PreventClose = true,
            }
        );

        if (!dialog.Cancelled)
        {
            await LoadContentsAsync();
        }
    }

    private async Task DeleteAsync(AdditionalArchiveContentReadModel content)
    {
        var confirmation = await dialogService.ConfirmAsync(
            L["DeleteNamedItem", content.Name],
            L["DeleteAdditionalArchiveContentConfirmation", content.Name],
            new ConfirmDialogOptions
            {
                ConfirmText = L["Delete"],
                CancelText = L["Cancel"],
                Destructive = true,
            }
        );

        if (!confirmation.Confirmed)
        {
            return;
        }

        var result = await operationRunner.RunAsync(
            (AdditionalArchiveContentService service) => service.DeleteAsync(content.Id)
        );

        if (!result.IsDeleted)
        {
            toastService.Error(DescribeUsage(content.Name, result));
            return;
        }

        toastService.Success(L["AdditionalArchiveContentDeleted", content.Name]);
        await LoadContentsAsync();
    }

    private string DescribeUsage(string contentName, AdditionalArchiveContentDeleteResult result)
    {
        var parts = new List<string> { L["AdditionalArchiveContentStillAssigned", contentName] };

        if (result.UsingReleaseTemplateNames.Count > 0)
        {
            parts.Add(
                L[
                    "AdditionalArchiveContentUsedByReleaseTemplates",
                    JoinNames(result.UsingReleaseTemplateNames)
                ]
            );
        }

        if (result.UsingReleaseNames.Count > 0)
        {
            parts.Add(
                L["AdditionalArchiveContentUsedByReleases", JoinNames(result.UsingReleaseNames)]
            );
        }

        return string.Join(" ", parts);
    }

    private string JoinNames(IReadOnlyList<string> names)
    {
        var listedNames = string.Join(", ", names.Take(MaximumListedUsageNames));

        return names.Count > MaximumListedUsageNames
            ? $"{listedNames} {L["AndCountMore", names.Count - MaximumListedUsageNames]}"
            : listedNames;
    }
}
