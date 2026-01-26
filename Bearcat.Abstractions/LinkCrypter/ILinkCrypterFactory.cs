namespace Bearcat.Abstractions.LinkCrypter;

public interface ILinkCrypterFactory
{
    IReadOnlyList<LinkCrypterDto> GetLinkCrypters();

    ILinkCrypter Get(string className);

    IReadOnlyDictionary<string, ILinkCrypter> GetByClassName();
}
