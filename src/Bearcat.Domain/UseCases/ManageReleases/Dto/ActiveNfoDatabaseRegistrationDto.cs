namespace Bearcat.Domain.UseCases.ManageReleases.Dto;

public record ActiveNfoDatabaseRegistrationDto(
    string NfoDatabaseClassName,
    string SerializedConfig
);
