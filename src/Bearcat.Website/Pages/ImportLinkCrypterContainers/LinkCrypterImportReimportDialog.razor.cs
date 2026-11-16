using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Exceptions;
using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;
using Bearcat.Website.Localization;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public partial class LinkCrypterImportReimportDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    [EditorRequired]
    public int LinkCrypterImportContainerId { get; set; }

    private LinkCrypterImportReimportPlanReadModel? plan;
    private string? errorMessage;
    private bool isReimporting;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            plan = await Task.Run(() =>
                operationRunner.RunAsync(
                    (LinkCrypterImportContainerReimportService service) =>
                        service.CreatePlanAsync(LinkCrypterImportContainerId)
                )
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            errorMessage = exception.Message;
        }
    }

    private async Task ReimportAsync()
    {
        isReimporting = true;
        errorMessage = null;

        try
        {
            var reimportPlan = await Task.Run(() =>
                operationRunner.RunAsync(
                    (LinkCrypterImportContainerReimportService service) =>
                        service.ReimportAsync(LinkCrypterImportContainerId)
                )
            );

            await DialogRef.CloseAsync(
                DialogResult.Ok(GetReleaseCountWithMovedArchiveParts(reimportPlan))
            );
        }
        catch (InvalidLinkCrypterImportException exception)
        {
            errorMessage = exception.Message;
        }
        finally
        {
            isReimporting = false;
        }
    }

    private static int GetReleaseCountWithMovedArchiveParts(
        LinkCrypterImportReimportPlanReadModel reimportPlan
    )
    {
        return reimportPlan.Releases.Count(release =>
            release.Preview.ArchiveFolderContainsArchivesOfOtherReleases
        );
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }

    private string GetMovedUploadsText(LinkCrypterImportReimportReleaseReadModel release)
    {
        if (release.MovedUploads.Count == 0)
        {
            return L["LinkCrypterImportReimportNoMovedUploads"];
        }

        var movedUploadTexts = release.MovedUploads.Select(movedUpload =>
            L[
                "LinkCrypterImportReimportMovedUpload",
                movedUpload.UploadConfigName,
                movedUpload.FileCount
            ].Value
        );

        return L["LinkCrypterImportReimportMovedUploads", string.Join(", ", movedUploadTexts)];
    }

    private string GetBlockerText(LinkCrypterImportReimportBlocker blocker)
    {
        return blocker.Reason switch
        {
            LinkCrypterImportReimportBlockerReason.NotImportable when blocker.Detail is null =>
                L.Localize(blocker.NotImportableReason!.Value),
            LinkCrypterImportReimportBlockerReason.NotImportable =>
                $"{blocker.Detail}: {L.Localize(blocker.NotImportableReason!.Value)}",
            _ => L[
                $"LinkCrypterImportReimportBlockerReason.{blocker.Reason}",
                blocker.Detail ?? ""
            ],
        };
    }
}
