using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using CsvHelper;

namespace Bearcat.Domain.UseCases.ManageReleases.LinkExport;

public static class ReleaseLinksCsvWriter
{
    private static readonly CsvWriterOptions Options = new()
    {
        Quoting = CsvQuoting.WhenNeededOrWhitespace,
    };

    public static async Task WriteAsync(
        Stream stream,
        IAsyncEnumerable<ReleaseLinkExportReadModel> links,
        CancellationToken cancellationToken
    )
    {
        await using var csvWriter = CsvWriter.Create(stream, Options, leaveOpen: true);

        await csvWriter.WriteRecordsAsync(links, cancellationToken);
    }
}
