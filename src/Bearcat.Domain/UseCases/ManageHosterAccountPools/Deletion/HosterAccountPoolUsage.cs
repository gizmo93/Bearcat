namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Deletion;

public record HosterAccountPoolUsage(int UploadConfigCount, int UploadConfigTemplateCount)
{
    public bool IsInUse => UploadConfigCount > 0 || UploadConfigTemplateCount > 0;
}
