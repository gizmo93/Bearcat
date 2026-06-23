using Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageQualityProfiles;

public class QualityProfileFormModel
{
    public string Name { get; set; } = string.Empty;

    public bool IsEdit { get; set; }

    public int? QualityProfileId { get; set; }

    public IReadOnlyList<QualityCheckRuleReadModel> Rules { get; set; } = [];
}

public class QualityCheckRuleEditModel
{
    public QualityCheckRuleType RuleType { get; set; }

    public Dictionary<string, object?> Parameters { get; set; } = [];
}
