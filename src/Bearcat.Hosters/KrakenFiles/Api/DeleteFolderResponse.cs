namespace Bearcat.Hosters.KrakenFiles.Api;

public record DeleteFolderResponse
{
    public int Status { get; init; }

    public DeleteFolderData? Data { get; init; }
}
