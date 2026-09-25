using System.Linq.Expressions;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.ReadModels;
using Bearcat.Domain.UseCases.ManageAdditionalArchiveContents.Repositories;
using Bearcat.Website.ScopedOperations;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageAdditionalArchiveContents.Assignment;

public partial class AdditionalArchiveContentMultiSelect(IScopedOperationRunner operationRunner)
    : ComponentBase
{
    [Parameter]
    public IEnumerable<int>? Values { get; set; }

    [Parameter]
    public EventCallback<IEnumerable<int>?> ValuesChanged { get; set; }

    [Parameter]
    public Expression<Func<IEnumerable<int>?>>? ValuesExpression { get; set; }

    private IReadOnlyList<AdditionalArchiveContentReadModel>? additionalArchiveContents;

    protected override async Task OnInitializedAsync()
    {
        additionalArchiveContents = await operationRunner.RunAsync(
            (IAdditionalArchiveContentReadRepository repository) => repository.GetAllAsync()
        );
    }
}
