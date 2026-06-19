using Bearcat.Hosters.Shared.XFilesharing;

namespace Bearcat.Hosters.ClicknUpload;

public record ClicknUploadConfig : IXFilesharingHosterConfig
{
    public string ApiKey { get; init; } = null!;

    public IReadOnlyDictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string> { [nameof(ApiKey)] = ApiKey };
    }
}
