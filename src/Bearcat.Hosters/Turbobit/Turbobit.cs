using Bearcat.Hosters.Shared.CostAction;
using Bearcat.Hosters.Shared.CostAction.Api;
using Microsoft.Extensions.Logging;

namespace Bearcat.Hosters.Turbobit;

public class Turbobit(ICostActionApiClient apiClient, ILogger<Turbobit> logger)
    : CostActionHosterBase<TurbobitConfig>(apiClient, logger)
{
    public override string Name => "turbobit.net";

    public override IReadOnlyList<string> FileLinkHostNames =>
        [
            "trbt.cc",
            "turbobit.net",
            "ifolder.com.ua",
            "turo-bit.net",
            "depositfiles.com.ua",
            "dlbit.net",
            "hotshare.biz",
            "sibit.net",
            "turbobit.ru",
            "xrfiles.ru",
            "turbabit.net",
            "filedeluxe.com",
            "filemaster.ru",
            "turboot.ru",
            "kilofile.com",
            "twobit.ru",
            "forum.flacmania.ru",
            "filhost.ru",
            "fayloobmennik.com",
            "rapidfile.tk",
            "turbo.to",
            "cccy.su",
            "turbo-bit.net",
            "turbobit.cc",
            "turbobit.pw",
            "turb.to",
            "turbo-bit.pw",
            "turbobit.cloud",
            "turbobit.online",
            "turbobit.live",
            "wayupload.com",
            "turb.cc",
            "turbobit5.net",
            "turbobith.net",
            "turbobi.pw",
            "turbobbit.com",
            "turbobyt.com",
            "trubobit.com",
            "turboget.net",
            "turbobiyt.net",
            "turbobif.com",
            "turbobite.net",
            "tourbobit.com",
            "turbobitn.com",
            "turbobeet.net",
            "turbobita.net",
            "turboobit.com",
            "turbobitt.net",
            "turbobyte.net",
            "torbobit.net",
        ];

    protected override string AppType => "fd1";

    protected override string OwnFileLinkFormat => "https://trbt.cc/{0}.html";
}
