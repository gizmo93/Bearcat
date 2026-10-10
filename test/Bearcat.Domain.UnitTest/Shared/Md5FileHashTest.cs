using System.Security.Cryptography;
using Bearcat.Abstractions.Transfers;
using Bearcat.Domain.Shared;
using Shouldly;

namespace Bearcat.Domain.UnitTest.Shared;

public class Md5FileHashTest
{
    private static readonly byte[] FileContent = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private string filePath = null!;

    [SetUp]
    public void SetUp()
    {
        filePath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        File.WriteAllBytes(filePath, FileContent);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public async Task ComputeHashWithShortestUnknownNullByteSuffixAsync_NoKnownHashes_AppendsOneNullByte()
    {
        // Arrange
        var expectedHash = ComputeMd5HexOfContentWithNullBytes(nullByteCount: 1);

        // Act
        var result = await Md5FileHash.ComputeHashWithShortestUnknownNullByteSuffixAsync(
            fullFileName: filePath,
            addHashIfUnknown: _ => true,
            progress: NullTransferProgress.Instance,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.NullByteCount.ShouldBe(1);
        result.Md5Hash.ShouldBe(expectedHash);
    }

    [Test]
    public async Task ComputeHashWithShortestUnknownNullByteSuffixAsync_FirstTwoSuffixHashesKnown_AppendsThreeNullBytes()
    {
        // Arrange
        var knownHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ComputeMd5HexOfContentWithNullBytes(nullByteCount: 1),
            ComputeMd5HexOfContentWithNullBytes(nullByteCount: 2),
        };
        var expectedHash = ComputeMd5HexOfContentWithNullBytes(nullByteCount: 3);

        // Act
        var result = await Md5FileHash.ComputeHashWithShortestUnknownNullByteSuffixAsync(
            fullFileName: filePath,
            addHashIfUnknown: knownHashes.Add,
            progress: NullTransferProgress.Instance,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.NullByteCount.ShouldBe(3);
        result.Md5Hash.ShouldBe(expectedHash);
        knownHashes.ShouldContain(expectedHash);
    }

    [Test]
    public async Task ComputeHashWithShortestUnknownNullByteSuffixAsync_HashesKnown_DoesNotModifyFile()
    {
        // Arrange
        var knownHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ComputeMd5HexOfContentWithNullBytes(nullByteCount: 1),
        };

        // Act
        await Md5FileHash.ComputeHashWithShortestUnknownNullByteSuffixAsync(
            fullFileName: filePath,
            addHashIfUnknown: knownHashes.Add,
            progress: NullTransferProgress.Instance,
            cancellationToken: CancellationToken.None
        );

        // Assert
        (await File.ReadAllBytesAsync(filePath)).ShouldBe(FileContent);
    }

    [Test]
    public async Task ComputeHashWithShortestUnknownNullByteSuffixAsync_HashesKnown_ReadsFileOnce()
    {
        // Arrange
        var knownHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ComputeMd5HexOfContentWithNullBytes(nullByteCount: 1),
            ComputeMd5HexOfContentWithNullBytes(nullByteCount: 2),
        };
        var progress = new RecordingTransferProgress();

        // Act
        await Md5FileHash.ComputeHashWithShortestUnknownNullByteSuffixAsync(
            fullFileName: filePath,
            addHashIfUnknown: knownHashes.Add,
            progress: progress,
            cancellationToken: CancellationToken.None
        );

        // Assert
        progress.ReportedBytes.Sum().ShouldBe(FileContent.Length);
    }

    private static string ComputeMd5HexOfContentWithNullBytes(int nullByteCount)
    {
        return Convert.ToHexString(MD5.HashData([.. FileContent, .. new byte[nullByteCount]]));
    }

    private sealed class RecordingTransferProgress : ITransferProgress
    {
        public List<long> ReportedBytes { get; } = [];

        public void BeginFile(long? totalBytes) { }

        public void ReportBytesTransferred(long bytes)
        {
            ReportedBytes.Add(bytes);
        }
    }
}
