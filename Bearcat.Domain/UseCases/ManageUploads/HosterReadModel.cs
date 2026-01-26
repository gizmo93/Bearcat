namespace Bearcat.Domain.UseCases.ManageUploads;

public record HosterReadModel(string Name, string HosterClassName, IReadOnlyList<string> ConfigurationKeys);
