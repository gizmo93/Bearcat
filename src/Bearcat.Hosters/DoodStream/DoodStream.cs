using Bearcat.Abstractions.Hoster;
using Bearcat.Hosters.DoodStream.Api;
using Bearcat.Hosters.Shared.XVideoSharing;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.DoodStream;

public class DoodStream(ApiClient apiClient, ILogger<DoodStream> logger)
    : XVideoSharingHosterBase<DoodStreamConfig>(apiClient, logger),
        IHosterWithFolders
{
    public override string Name => "DoodStream";

    public override IReadOnlyList<string> FileLinkHostNames =>
        [
            "doodstream.com",
            "dood.re",
            "doods.pro",
            "dood.so",
            "dood.to",
            "doodapi.com",
            "dood.watch",
            "dood.cx",
            "doodstream.co",
            "dood.la",
            "dood.ws",
            "dood.pm",
            "dood.sh",
            "dood.one",
            "dood.tech",
            "dood.wf",
            "dood.yt",
            "dooood.com",
            "ds2play.com",
            "ds2video.com",
            "d0o0d.com",
            "do0od.com",
            "d0000d.com",
            "d000d.com",
            "dood.li",
            "dood.work",
            "dooodster.com",
            "vidply.com",
            "do7go.com",
            "all3do.com",
            "doply.net",
            "vide0.net",
            "vvide0.com",
            "d-s.io",
            "dsvplay.com",
            "myvidplay.com",
            "playmogo.com",
        ];
}
