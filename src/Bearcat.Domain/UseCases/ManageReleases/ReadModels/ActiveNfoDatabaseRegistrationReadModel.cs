namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ActiveNfoDatabaseRegistrationReadModel(
    string NfoDatabaseClassName,
    string SerializedConfig
);
