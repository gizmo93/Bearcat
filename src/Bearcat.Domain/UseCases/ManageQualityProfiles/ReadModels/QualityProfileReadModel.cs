namespace Bearcat.Domain.UseCases.ManageQualityProfiles.ReadModels;

public record QualityProfileReadModel(int Id, string Name, int RuleCount, int AssignedGroupCount);
