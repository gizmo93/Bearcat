namespace Bearcat.Domain.ValueObjects;

public enum CollectionUploadSlotPasswordPolicy
{
    Ignore = 1,
    MustMatchAcrossReleases = 2,
    MustEqualExpectedValue = 3,
}
