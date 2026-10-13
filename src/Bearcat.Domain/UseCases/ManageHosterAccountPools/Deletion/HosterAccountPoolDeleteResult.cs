namespace Bearcat.Domain.UseCases.ManageHosterAccountPools.Deletion;

public record HosterAccountPoolDeleteResult(bool IsDeleted, HosterAccountPoolUsage Usage);
