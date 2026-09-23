namespace Bearcat.Domain.IntegrationTest.UseCases.AutomateReleaseCreation.RemoteSources;

public sealed record FakeRemoteFile(string RelativePath, long SizeBytes);
