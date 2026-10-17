namespace Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;

public sealed class InvalidUploadSourceException(string message) : Exception(message);
