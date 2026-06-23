using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;

public record QualityCheckRuleReadModel(QualityCheckRuleType RuleType, string ParametersJson);

public record QualityProfileDetailReadModel(
    int Id,
    string Name,
    IReadOnlyList<QualityCheckRuleReadModel> Rules
);
