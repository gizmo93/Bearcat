namespace Bearcat.Domain.ValueObjects;

public enum QualityCheckRuleType
{
    FilePatternPresent = 1,
    MinimumFolderSize = 2,
    RequiredReleaseInfo = 3,
    MediaInfoPresent = 4,
}
