namespace Bearcat.Domain.UseCases.ManageRemoteSources;

public record RemoteSourceConnectionTestResult(
    bool IsSuccess,
    string? ErrorMessage,
    int RootFolderCount
);
