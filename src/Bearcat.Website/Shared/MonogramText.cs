namespace Bearcat.Website.Shared;

public static class MonogramText
{
    public static string GetFromName(string name) => name.Length <= 2 ? name : name[..2];
}
