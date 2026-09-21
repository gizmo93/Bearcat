using Refit;

namespace Bearcat.ImageHosters.LoePic.Api;

public record UrlUploadRequest([property: AliasAs("url")] string Url);
