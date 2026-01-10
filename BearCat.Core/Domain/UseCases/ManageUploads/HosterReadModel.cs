namespace BearCat.Core.Domain.UseCases.ManageUploads;

public record HosterReadModel(string Name, string FullClassName, IReadOnlyList<string> ConfigurationKeys);
