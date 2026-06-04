namespace Bearcat.Abstractions.LinkCrypter;

public record LinkCrypterDto(
    string Name,
    string ClassName,
    List<string> ConfigurationKeys,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad
);
