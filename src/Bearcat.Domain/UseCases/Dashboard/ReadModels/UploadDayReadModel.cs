namespace Bearcat.Domain.UseCases.Dashboard.ReadModels;

public sealed record UploadDayReadModel(DateOnly Day, string HosterName, int UploadCount);
