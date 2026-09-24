using System.Buffers;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.RemoteSources.Scanning;

public static class RemoteFolderNameValidator
{
    private static readonly SearchValues<char> ForbiddenCharacters = SearchValues.Create([
        .. Path.GetInvalidFileNameChars(),
        '/',
        '\\',
    ]);

    public static bool IsSafe(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName) || folderName.All(character => character == '.'))
        {
            return false;
        }

        return !folderName.AsSpan().ContainsAny(ForbiddenCharacters)
            && !Path.IsPathRooted(folderName);
    }
}
