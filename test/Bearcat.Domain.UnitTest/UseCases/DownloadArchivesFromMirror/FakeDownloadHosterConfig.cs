using Bearcat.Abstractions.Hoster;

namespace Bearcat.Domain.UnitTest.UseCases.DownloadArchivesFromMirror;

public sealed record FakeDownloadHosterConfig : IHosterConfig
{
    public IReadOnlyDictionary<string, string> ToDictionary() => new Dictionary<string, string>();
}
