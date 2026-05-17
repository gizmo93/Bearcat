namespace Bearcat.Domain.UseCases.ManageUploadConfigLinkCrypters.Dto;

public record UploadConfigLinkCrypterDto(
    int UploadConfigLinkCrypterId,
    string LinkCrypterName,
    string LinkCrypterRegistrationName,
    int LinkCrypterRegistrationId,
    string? Password,
    bool LinkCrypterIsActive
);
