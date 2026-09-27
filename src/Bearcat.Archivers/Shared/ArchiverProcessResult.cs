namespace Bearcat.Archivers.Shared;

public record ArchiverProcessResult(
    int ExitCode,
    IReadOnlyList<string> OutputLines,
    IReadOnlyList<string> ErrorLines
)
{
    public IReadOnlyList<string> ErrorLinesOrOutputLinesWhenNoErrorLines =>
        ErrorLines.Count > 0 ? ErrorLines : OutputLines;
}
