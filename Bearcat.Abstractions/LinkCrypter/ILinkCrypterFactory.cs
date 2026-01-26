namespace Bearcat.Abstractions.LinkCrypter;

public interface ILinkCrypterFactory
{
    public IReadOnlyList<LinkCrypterDto> GetLinkCrypters();
    
    public ILinkCrypter GetByClassName(string className);
}
