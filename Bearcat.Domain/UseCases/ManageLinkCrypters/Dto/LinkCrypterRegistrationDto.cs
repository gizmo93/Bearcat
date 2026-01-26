namespace Bearcat.Domain.UseCases.ManageLinkCrypters.Dto;

public record LinkCrypterRegistrationDto(
    int LinkCrypterRegistrationId,
    string Name,
    string LinkCrypterClassName,
    string CrypterName,
    string SerializedConfig,
    IReadOnlyDictionary<string, string> Configuration,
    bool IsActive);
