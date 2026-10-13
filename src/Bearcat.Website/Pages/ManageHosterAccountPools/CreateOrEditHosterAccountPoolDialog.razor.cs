using Bearcat.Domain.UseCases.ManageHosterAccountPools;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.ReadModels;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Repositories;
using Bearcat.Domain.UseCases.ManageHosterAccountPools.Validation;
using Bearcat.Website.ScopedOperations;
using Bearcat.Website.Shared;
using BlazorBlueprint.Components;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Bearcat.Website.Pages.ManageHosterAccountPools;

public partial class CreateOrEditHosterAccountPoolDialog(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [CascadingParameter]
    public IDialogReference DialogRef { get; set; } = null!;

    [Parameter]
    public HosterAccountPoolFormModel FormModel { get; set; } = new();

    private EditContext editContext = null!;
    private ValidationMessageStore messageStore = null!;
    private IReadOnlyList<PoolableHosterClassReadModel> poolableHosterClasses = [];
    private IReadOnlyList<HosterAccountPoolCandidateReadModel> candidates = [];
    private int? candidateToAddId;
    private bool isSaving;

    private bool IsEdit => FormModel.HosterAccountPoolId is not null;

    private IReadOnlyList<SelectOption<string>> HosterClassOptions =>
        IsEdit
            ? [new SelectOption<string>(FormModel.HosterClassName!, FormModel.HosterName!)]
            : poolableHosterClasses
                .Select(hosterClass => new SelectOption<string>(
                    hosterClass.HosterClassName,
                    hosterClass.HosterName
                ))
                .ToList();

    private List<HosterAccountPoolCandidateReadModel> AvailableCandidates =>
        candidates
            .Where(candidate =>
                FormModel.MembersInPriorityOrder.TrueForAll(member =>
                    member.HosterRegistrationId != candidate.HosterRegistrationId
                )
            )
            .ToList();

    private IReadOnlyList<SelectOption<int?>> CandidateOptions =>
        AvailableCandidates
            .Select(candidate => new SelectOption<int?>(
                candidate.HosterRegistrationId,
                candidate.IsActive
                    ? candidate.Name
                    : L["InactiveAccountOption", candidate.Name].Value
            ))
            .ToList();

    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(FormModel);
        messageStore = new ValidationMessageStore(editContext);
        editContext.OnValidationRequested += (_, _) => messageStore.Clear();
        editContext.OnFieldChanged += (_, args) => ClearValidationMessages(args.FieldIdentifier);

        if (IsEdit)
        {
            await LoadCandidatesAsync();
            return;
        }

        poolableHosterClasses = await operationRunner.RunAsync(
            (HosterAccountPoolService service) => service.GetPoolableHosterClassesAsync()
        );
    }

    private async Task ChangeHosterClassAsync(string? hosterClassName)
    {
        if (hosterClassName == FormModel.HosterClassName)
        {
            return;
        }

        FormModel.HosterClassName = hosterClassName;
        FormModel.MembersInPriorityOrder.Clear();
        candidateToAddId = null;
        candidates = [];

        if (hosterClassName is not null)
        {
            await LoadCandidatesAsync();
        }
    }

    private async Task LoadCandidatesAsync()
    {
        var hosterClassName = FormModel.HosterClassName!;

        candidates = await operationRunner.RunAsync(
            (IHosterAccountPoolReadRepository repository) =>
                repository.GetCandidateRegistrationsAsync(hosterClassName)
        );
    }

    private void AddMember()
    {
        var candidate = candidates.First(candidate =>
            candidate.HosterRegistrationId == candidateToAddId
        );

        FormModel.MembersInPriorityOrder.Add(candidate);
        candidateToAddId = null;
    }

    private void RemoveMember(HosterAccountPoolCandidateReadModel member)
    {
        FormModel.MembersInPriorityOrder.Remove(member);
    }

    private void MoveMember((int OldIndex, int NewIndex) move)
    {
        SortableListReordering.TryMoveItem(FormModel.MembersInPriorityOrder, move);
    }

    private void ClearValidationMessages(FieldIdentifier field)
    {
        if (!editContext.GetValidationMessages(field).Any())
        {
            return;
        }

        messageStore.Clear(field);
        editContext.NotifyValidationStateChanged();
    }

    private async Task SaveAsync()
    {
        isSaving = true;

        var input = FormModel.ToInput();
        var hosterClassName = FormModel.HosterClassName!;

        HosterAccountPoolSaveResult result;

        try
        {
            result = FormModel.HosterAccountPoolId is { } hosterAccountPoolId
                ? await operationRunner.RunAsync(
                    (HosterAccountPoolService service) =>
                        service.UpdateAsync(hosterAccountPoolId, input)
                )
                : await operationRunner.RunAsync(
                    (HosterAccountPoolService service) =>
                        service.CreateAsync(hosterClassName, input)
                );
        }
        finally
        {
            isSaving = false;
        }

        if (!result.IsSuccess)
        {
            ShowValidationErrors(result.ValidationErrors);
            return;
        }

        await DialogRef.CloseAsync(DialogResult.Ok(result.HosterAccountPoolId));
    }

    private void ShowValidationErrors(
        IReadOnlyList<HosterAccountPoolValidationError> validationErrors
    )
    {
        messageStore.Clear();

        foreach (var validationError in validationErrors)
        {
            messageStore.Add(
                editContext.Field(GetFieldName(validationError)),
                L[$"HosterAccountPoolValidationError.{validationError}"]
            );
        }

        editContext.NotifyValidationStateChanged();
    }

    private static string GetFieldName(HosterAccountPoolValidationError validationError)
    {
        return validationError switch
        {
            HosterAccountPoolValidationError.MinimumFreeSpaceNegative => nameof(
                HosterAccountPoolFormModel.MinimumFreeSpaceGb
            ),
            _ => nameof(HosterAccountPoolFormModel.Name),
        };
    }

    private async Task CancelAsync()
    {
        await DialogRef.CancelAsync();
    }
}
