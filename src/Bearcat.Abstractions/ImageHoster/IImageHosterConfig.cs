namespace Bearcat.Abstractions.ImageHoster;

public interface IImageHosterConfig
{
    IReadOnlyDictionary<string, string> ToDictionary();
}
