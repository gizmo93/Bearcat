using System.Text;
using Bearcat.Domain.UseCases.ManageReleases.LinkExport;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Domain.ValueObjects;
using Shouldly;

namespace Bearcat.Domain.UnitTest.UseCases.ManageReleases.LinkExport;

public class ReleaseLinksCsvWriterTest
{
    private const string HeaderRow =
        "Release,ReleaseGroup,Hoster,UploadId,UploadOnlineState,FileName,Link,EmbedLink,LinkOnlineState,CheckedAt,ArchivePassword\r\n";

    [Test]
    public async Task WriteAsync_CompleteLink_WritesHeaderAndRowWithoutByteOrderMark()
    {
        // Arrange
        var link = new ReleaseLinkExportReadModel(
            ReleaseName: "Bearcat.Release.2026-GRP",
            ReleaseGroupName: "GRP",
            HosterName: "Rapidgator",
            UploadId: 42,
            UploadOnlineState: OnlineState.PartiallyOnline,
            FileName: "bearcat.part1.rar",
            Link: "https://hoster.example/file",
            EmbedLink: "https://hoster.example/embed",
            LinkOnlineState: OnlineState.Offline,
            CheckedAt: new DateTime(2026, 3, 1, 14, 5, 9, DateTimeKind.Utc),
            ArchivePassword: "secret"
        );

        // Act
        var bytes = await WriteToBytesAsync(link);

        // Assert
        bytes.ShouldBe(
            Encoding.UTF8.GetBytes(
                HeaderRow
                    + "Bearcat.Release.2026-GRP,GRP,Rapidgator,42,PartiallyOnline,bearcat.part1.rar,https://hoster.example/file,https://hoster.example/embed,Offline,2026-03-01 14:05:09,secret\r\n"
            )
        );
    }

    [TestCase("with,comma", "\"with,comma\"")]
    [TestCase(" leading space", "\" leading space\"")]
    public async Task WriteAsync_ValueWithSpecialCharacters_QuotesOnlyWhenRequired(
        string archivePassword,
        string expectedCsvValue
    )
    {
        // Arrange
        var link = CreateLinkWithOptionalValuesEmpty() with
        {
            ArchivePassword = archivePassword,
        };

        // Act
        var csv = Encoding.UTF8.GetString(await WriteToBytesAsync(link));

        // Assert
        csv.ShouldBe(
            HeaderRow
                + $"Release,Group,Hoster,1,Online,file.rar,https://hoster.example/file,,Online,,{expectedCsvValue}\r\n"
        );
    }

    [Test]
    public async Task WriteAsync_StreamWithoutSynchronousWrites_WritesAllRows()
    {
        // Arrange
        const int rowCount = 5000;
        var links = Enumerable
            .Repeat(CreateLinkWithOptionalValuesEmpty(), rowCount)
            .ToAsyncEnumerable();
        using var stream = new AsynchronousWritesOnlyStream();

        // Act
        await ReleaseLinksCsvWriter.WriteAsync(stream, links, CancellationToken.None);

        // Assert
        var rows = Encoding.UTF8.GetString(stream.ToArray()).Split("\r\n");
        rows.Length.ShouldBe(rowCount + 2);
    }

    private static ReleaseLinkExportReadModel CreateLinkWithOptionalValuesEmpty()
    {
        return new ReleaseLinkExportReadModel(
            ReleaseName: "Release",
            ReleaseGroupName: "Group",
            HosterName: "Hoster",
            UploadId: 1,
            UploadOnlineState: OnlineState.Online,
            FileName: "file.rar",
            Link: "https://hoster.example/file",
            EmbedLink: null,
            LinkOnlineState: OnlineState.Online,
            CheckedAt: null,
            ArchivePassword: null
        );
    }

    private static async Task<byte[]> WriteToBytesAsync(ReleaseLinkExportReadModel link)
    {
        using var stream = new MemoryStream();

        await ReleaseLinksCsvWriter.WriteAsync(
            stream,
            new[] { link }.ToAsyncEnumerable(),
            CancellationToken.None
        );

        return stream.ToArray();
    }

    private sealed class AsynchronousWritesOnlyStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Synchronous writes are not allowed");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new InvalidOperationException("Synchronous writes are not allowed");
        }

        public override void Flush()
        {
            throw new InvalidOperationException("Synchronous flushes are not allowed");
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            base.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            base.Write(buffer.ToArray(), 0, buffer.Length);
            return ValueTask.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
