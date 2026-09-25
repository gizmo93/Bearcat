using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageAdditionalArchiveContents.Assignment;

public static class AdditionalArchiveContentTypeIconNames
{
    public static string GetIconName(AdditionalArchiveContentType type)
    {
        return type == AdditionalArchiveContentType.Path ? "folder" : "file-text";
    }
}
