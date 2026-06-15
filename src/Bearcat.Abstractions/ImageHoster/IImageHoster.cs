using Bearcat.Abstractions.ImageHoster.Dto;
using Bearcat.Abstractions.ImageHoster.Results;

namespace Bearcat.Abstractions.ImageHoster;

public interface IImageHoster
{
    string Name { get; }

    IReadOnlyList<string> ConfigurationKeys { get; }

    Task<UploadImageResult> UploadImageAsync(
        ImageToUploadDto image,
        IImageHosterConfig imageHosterConfig,
        CancellationToken cancellationToken = default
    );

    string SerializeConfig(IReadOnlyDictionary<string, string> config);

    IImageHosterConfig DeserializeConfig(string serializedConfig);
}
