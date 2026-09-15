namespace Bearcat.Domain.UseCases.PostToForums;

public static class ReleaseNameFormatter
{
    public static string ToSpacedName(string releaseName)
    {
        var spaced = releaseName.Replace('.', ' ');

        var lastHyphen = spaced.LastIndexOf('-');

        if (lastHyphen > 0 && lastHyphen < spaced.Length - 1)
        {
            var beforeGroup = spaced[..lastHyphen].TrimEnd();
            var group = spaced[(lastHyphen + 1)..].TrimStart();

            return $"{beforeGroup} - {group}";
        }

        return spaced;
    }
}
