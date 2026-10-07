namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.ArchiveExtraction.SfvVerification;

public record SfvFile(string FilePath, IReadOnlyList<SfvEntry> Entries);
