using Bearcat.Abstractions.NfoDatabase;

namespace Bearcat.Domain.Entities;

public class ReleaseExternalInfo
{
    public int Id { get; set; }

    public int ReleaseInfoId { get; set; }

    public ReleaseInfo ReleaseInfo { get; set; } = null!;

    public ExternalInfoType Type { get; set; }

    public string? Title { get; set; }

    public List<ReleaseExternalInfoUrl> Urls { get; set; } = [];
}

public class ReleaseExternalInfoUrl
{
    public UrlType Type { get; set; }

    public string Url { get; set; } = null!;
}
