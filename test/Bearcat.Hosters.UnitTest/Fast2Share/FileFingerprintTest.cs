using System.Security.Cryptography;
using Bearcat.Hosters.Fast2Share.Api;
using Shouldly;

namespace Bearcat.Hosters.UnitTest.Fast2Share;

public class FileFingerprintTest
{
    private const int SegmentSize = 8 * 1024 * 1024;

    private string filePath = null!;

    [SetUp]
    public void SetUp()
    {
        filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bin");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(filePath);
    }

    [Test]
    public async Task ComputeFingerprintAsync_FileSmallerThanOneSegment_HashesWholeFile()
    {
        // Arrange
        var content = CreateContent(1024);
        await File.WriteAllBytesAsync(filePath, content);

        // Act
        var result = await FileFingerprint.ComputeFingerprintAsync(
            filePath,
            TestContext.CurrentContext.CancellationToken
        );

        // Assert
        result.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    [Test]
    public async Task ComputeFingerprintAsync_FileBetweenOneAndTwoSegments_CoversFileWithoutOverlap()
    {
        // Arrange
        var content = CreateContent(SegmentSize + (1024 * 1024));
        await File.WriteAllBytesAsync(filePath, content);

        // Act
        var result = await FileFingerprint.ComputeFingerprintAsync(
            filePath,
            TestContext.CurrentContext.CancellationToken
        );

        // Assert
        result.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    [Test]
    public async Task ComputeFingerprintAsync_FileLargerThanTwoSegments_HashesFirstAndLastSegment()
    {
        // Arrange
        var content = CreateContent((2 * SegmentSize) + (1024 * 1024));
        await File.WriteAllBytesAsync(filePath, content);

        var expected = Convert.ToHexStringLower(
            SHA256.HashData([
                .. content.AsSpan(0, SegmentSize),
                .. content.AsSpan(content.Length - SegmentSize, SegmentSize),
            ])
        );

        // Act
        var result = await FileFingerprint.ComputeFingerprintAsync(
            filePath,
            TestContext.CurrentContext.CancellationToken
        );

        // Assert
        result.ShouldBe(expected);
    }

    [Test]
    public async Task ComputeSha256Async_File_ReturnsLowercaseHexHash()
    {
        // Arrange
        var content = CreateContent(4096);
        await File.WriteAllBytesAsync(filePath, content);

        // Act
        var result = await FileFingerprint.ComputeSha256Async(
            filePath,
            TestContext.CurrentContext.CancellationToken
        );

        // Assert
        result.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(content)));
    }

    private static byte[] CreateContent(int length)
    {
        var content = new byte[length];

        for (var index = 0; index < length; index++)
        {
            content[index] = (byte)(index % 251);
        }

        return content;
    }
}
