namespace Bearcat.Domain.UseCases.ManageUploads.Repositories;

public record HosterUploadConcurrencyInfo(string SerializedConfig, int? MaxParallelUploadsOverride);
