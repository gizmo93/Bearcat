namespace BearCat.Core.Hosters.Rapidgator;

public record RapidgatorConfig : IHosterConfig
{
    public string Username { get; init; } = null!;

    public string Password { get; init; } = null!;
}