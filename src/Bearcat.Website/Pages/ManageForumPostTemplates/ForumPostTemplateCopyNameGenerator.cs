using System.Globalization;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public static class ForumPostTemplateCopyNameGenerator
{
    private const int FirstNumberedCopy = 2;

    public static string CreateCopyName(
        string sourceName,
        IReadOnlyCollection<string> existingNames,
        string copyNameFormat,
        string numberedCopyNameFormat
    )
    {
        var takenNames = existingNames.ToHashSet(StringComparer.Ordinal);
        var copyName = string.Format(CultureInfo.CurrentCulture, copyNameFormat, sourceName);
        var copyNumber = FirstNumberedCopy;

        while (takenNames.Contains(copyName))
        {
            copyName = string.Format(
                CultureInfo.CurrentCulture,
                numberedCopyNameFormat,
                sourceName,
                copyNumber
            );
            copyNumber++;
        }

        return copyName;
    }
}
