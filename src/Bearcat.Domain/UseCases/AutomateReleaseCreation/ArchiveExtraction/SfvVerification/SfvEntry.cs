namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;

public record SfvEntry(string FilePath, string RelativeFilePath, uint ExpectedCrc32);
