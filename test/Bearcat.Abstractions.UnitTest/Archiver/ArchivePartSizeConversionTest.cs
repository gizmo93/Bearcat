using Bearcat.Abstractions.Archiver;
using Shouldly;

namespace Bearcat.Abstractions.UnitTest.Archiver;

public class ArchivePartSizeConversionTest
{
    [TestCase(100, ArchivePartSizeUnit.Megabytes, 100)]
    [TestCase(100, ArchivePartSizeUnit.Mebibytes, 95)]
    [TestCase(2000, ArchivePartSizeUnit.Mebibytes, 1907)]
    public void ConvertMegabytesToPartSizeUnit_HosterLimitInMegabytes_ReturnsLargestFittingPartSize(
        int megabytes,
        ArchivePartSizeUnit partSizeUnit,
        int expectedPartSize
    )
    {
        // Act
        var partSize = ArchivePartSizeConversion.ConvertMegabytesToPartSizeUnit(
            megabytes: megabytes,
            partSizeUnit: partSizeUnit
        );

        // Assert
        partSize.ShouldBe(expectedPartSize);
    }
}
