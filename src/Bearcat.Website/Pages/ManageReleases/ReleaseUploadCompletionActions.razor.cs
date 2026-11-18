using Bearcat.Domain.UseCases.RunUploadCompletionActions;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.ReadModels;
using Bearcat.Domain.UseCases.RunUploadCompletionActions.Repositories;
using Bearcat.Website.ScopedOperations;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseUploadCompletionActions(
    IScopedOperationRunner operationRunner,
    ToastService toastService
) : ComponentBase
{
    [Parameter]
    public int ReleaseId { get; set; }

    private readonly HashSet<int> runningRegistrationIds = [];
    private IReadOnlyList<UploadCompletionActionReleaseStateReadModel> states = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadStatesAsync();
    }

    private async Task LoadStatesAsync()
    {
        states = await operationRunner.RunAsync(
            (IUploadCompletionActionReleaseStateReadRepository repository) =>
                repository.GetReleaseStatesAsync(ReleaseId)
        );
    }

    private async Task RunNowAsync(UploadCompletionActionReleaseStateReadModel state)
    {
        runningRegistrationIds.Add(state.RegistrationId);

        try
        {
            await operationRunner.RunAsync(
                (UploadCompletionActionRunner runner) =>
                    runner.RunForReleaseAsync(state.RegistrationId, ReleaseId)
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
