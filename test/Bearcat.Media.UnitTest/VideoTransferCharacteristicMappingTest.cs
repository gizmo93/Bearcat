using Bearcat.Abstractions.Media;
using Shouldly;

namespace Bearcat.Media.UnitTest;

public class VideoTransferCharacteristicMappingTest
{
    [TestCase("PQ", VideoTransferCharacteristic.PerceptualQuantizer)]
    [TestCase("HLG", VideoTransferCharacteristic.HybridLogGamma)]
    [TestCase("BT.709", VideoTransferCharacteristic.StandardDynamicRange)]
    [TestCase(null, VideoTransferCharacteristic.StandardDynamicRange)]
    public void FromMediaInfoTransferCharacteristics_MapsMediaInfoValue(
        string? transferCharacteristics,
        VideoTransferCharacteristic expectedTransferCharacteristic
    )
    {
        // Act
        var transferCharacteristic =
            VideoTransferCharacteristicMapping.FromMediaInfoTransferCharacteristics(
                transferCharacteristics
            );

        // Assert
        transferCharacteristic.ShouldBe(expectedTransferCharacteristic);
    }
}
