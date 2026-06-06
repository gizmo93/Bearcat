using Bearcat.Abstractions.ImageHoster.Dto;

namespace Bearcat.Abstractions.ImageHoster;

public interface IImageHosterFactory
{
    IReadOnlyList<ImageHosterDto> GetImageHosters();

    IImageHoster Get(string className);

    IReadOnlyDictionary<string, IImageHoster> GetByClassName();
}
