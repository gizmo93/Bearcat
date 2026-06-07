namespace Bearcat.Domain.ValueObjects;

public enum CollectionUploadSlotPasswordPolicy
{
    Ignore = 0,
    MustMatchAcrossReleases = 1,
    MustEqualExpectedValue = 2,
}
