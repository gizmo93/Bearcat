namespace Bearcat.Domain.UseCases.ManageLinkCrypters.ReadModels;

public record LinkCrypterRegistrationReadModel(
    int LinkCrypterRegistrationId,
    string Name,
    string LinkCrypterClassName,
    string CrypterName,
    string SerializedConfig,
    IReadOnlyDictionary<string, string> Configuration,
    bool IsActive
);
