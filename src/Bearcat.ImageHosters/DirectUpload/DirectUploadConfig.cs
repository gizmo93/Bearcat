using Bearcat.Abstractions.ImageHoster;

namespace Bearcat.ImageHosters.DirectUpload;

public record DirectUploadConfig : IImageHosterConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>();
    }
}
