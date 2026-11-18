using Bearcat.Domain.UseCases.RunUploadCompletionActions;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageUploadCompletionActions;

public partial class UploadCompletionActionStates(
    IScopedOperationRunner operationRunner,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    public int? ReleaseId { get; set; }

    [Parameter]
    public int? ReleaseCollectionId { get; set; }

    private readonly HashSet<int> runningRegistrationIds = [];
    private IReadOnlyList<UploadCompletionActionStateReadModel> states = [];

    private string RunNowUnavailableText =>
        ReleaseCollectionId is null
            ? L["RunUploadCompletionActionNowUnavailable"]
            : L["RunUploadCompletionActionNowUnavailableForReleaseCollection"];

    protected override async Task OnInitializedAsync()
    {
        await LoadStatesAsync();
    }

    private async Task LoadStatesAsync()
    {
        states = await operationRunner.RunAsync(
            (IUploadCompletionActionStateReadRepository repository) =>
                ReleaseCollectionId is { } releaseCollectionId
                    ? repository.GetReleaseCollectionStatesAsync(releaseCollectionId)
                    : repository.GetReleaseStatesAsync(ReleaseId!.Value)
        );
    }

    private async Task RunNowAsync(UploadCompletionActionStateReadModel state)
    {
        runningRegistrationIds.Add(state.RegistrationId);

        try
        {
            await operationRunner.RunAsync(
                (UploadCompletionActionRunner runner) =>
                    ReleaseCollectionId is { } releaseCollectionId
                        ? runner.RunForReleaseCollectionAsync(
                            state.RegistrationId,
                            releaseCollectionId
                        )
                        : runner.RunForReleaseAsync(state.RegistrationId, ReleaseId!.Value)
            );

            await LoadStatesAsync();
        }
        finally
        {
            runningRegistrationIds.Remove(state.RegistrationId);
        }

        var updatedState = states.Single(updated => updated.RegistrationId == state.RegistrationId);

        if (updatedState.ErrorMessage is null)
        {
            toastService.Success(L["UploadCompletionActionSucceeded", state.RegistrationName]);
            return;
        }

        toastService.Error(L["UploadCompletionActionFailed", state.RegistrationName]);
    }
}
