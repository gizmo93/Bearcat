namespace Bearcat.Domain.UseCases.FirstStartSetup.Status;

public sealed record WorkingDirectoryStatus(string Path, bool Exists, bool IsWritable);
