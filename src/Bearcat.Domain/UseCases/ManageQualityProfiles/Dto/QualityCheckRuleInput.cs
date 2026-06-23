using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageQualityProfiles.Dto;

public record QualityCheckRuleInput(QualityCheckRuleType RuleType, string ParametersJson);
