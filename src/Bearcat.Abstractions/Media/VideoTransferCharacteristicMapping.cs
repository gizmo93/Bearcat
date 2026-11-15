namespace Bearcat.Abstractions.Media;

public static class VideoTransferCharacteristicMapping
{
    public static VideoTransferCharacteristic FromMediaInfoTransferCharacteristics(
        string? transferCharacteristics
    )
    {
        return transferCharacteristics switch
        {
            "PQ" => VideoTransferCharacteristic.PerceptualQuantizer,
            "HLG" => VideoTransferCharacteristic.HybridLogGamma,
            _ => VideoTransferCharacteristic.StandardDynamicRange,
        };
    }
}
