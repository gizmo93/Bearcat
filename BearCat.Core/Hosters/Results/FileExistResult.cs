namespace BearCat.Core.Hosters.Results;

public record FileExistResult(
    bool IsSuccess,
    IReadOnlyList<string> ErrorMessages,
    IReadOnlyDictionary<string, bool> StatusPerFileUrl);