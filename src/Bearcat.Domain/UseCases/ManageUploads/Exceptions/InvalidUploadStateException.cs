namespace Bearcat.Domain.UseCases.ManageUploads.Exceptions;

public sealed class InvalidUploadStateException(string message) : Exception(message);
