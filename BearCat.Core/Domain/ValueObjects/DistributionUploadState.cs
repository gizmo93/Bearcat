namespace BearCat.Core.Domain.ValueObjects;

public enum DistributionUploadState
{
    Unprocessed = 1,
    CreatingArchives = 2,
    Uploading = 3,
    Completed = 4,
    Failed = 5,
}
