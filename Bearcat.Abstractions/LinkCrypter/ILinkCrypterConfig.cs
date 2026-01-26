namespace Bearcat.Abstractions.LinkCrypter;

public interface ILinkCrypterConfig
{
    IReadOnlyDictionary<string, string> ToDictionary();
}
