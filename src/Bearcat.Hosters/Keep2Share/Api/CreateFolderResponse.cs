namespace Bearcat.Hosters.Keep2Share.Api;

public record CreateFolderResponse
{
    public string? Status { get; init; }

    public int Code { get; init; }

    public string? Id { get; init; }

    public string? Message { get; init; }
}
