using System.Globalization;
using Bearcat.Abstractions.Media;

namespace Bearcat.Media.Screenshots;

public static class FfmpegArgumentBuilder
{
    private const string AnamorphicCorrectionFilter = "scale=trunc(iw*sar/2)*2:ih,setsar=1";

    private const string PerceptualQuantizerLinearizationFilter =
        "zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=linear:npl=100:p=bt2020";

    private const string HybridLogGammaLinearizationFilter =
        "zscale=tin=arib-std-b67:min=bt2020nc:pin=bt2020:rin=tv:t=linear:npl=100:p=bt2020";

    private const string ToneMappingToBt709Filter =
        "format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

    private const string BackgroundColor = "0x202020";

    public static List<string> BuildScreenshotArguments(
        string videoFilePath,
        TimeSpan timestamp,
        VideoTransferCharacteristic transferCharacteristic,
        string outputFilePath
    )
    {
        return BuildFrameExtractionArguments(
            videoFilePath: videoFilePath,
            timestamp: timestamp,
            videoFilter: BuildFrameFilter(transferCharacteristic),
            outputFilePath: outputFilePath
        );
    }

    public static List<string> BuildScreenshotGridTileArguments(
        string videoFilePath,
        TimeSpan timestamp,
        VideoTransferCharacteristic transferCharacteristic,
        ScreenshotGridLayout layout,
        string timestampTextFileName,
        string outputFileName
    )
    {
        var timestampInset = layout.TimestampFontSize / 3;

        var timestampFilter = string.Join(
            ':',
            $"drawtext=fontfile={ScreenshotGridFont.FileName}",
            $"textfile={timestampTextFileName}",
            "expansion=none",
            "fontcolor=white",
            $"fontsize={layout.TimestampFontSize}",
            "box=1",
            "boxcolor=black@0.6",
            $"boxborderw={timestampInset}",
            $"x=w-tw-{timestampInset * 2}",
            $"y=h-th-{timestampInset * 2}"
        );

        var videoFilter = string.Join(
            ',',
            BuildFrameFilter(transferCharacteristic),
            $"scale={layout.TileWidth}:-2",
            timestampFilter
        );

        return BuildFrameExtractionArguments(
            videoFilePath: videoFilePath,
            timestamp: timestamp,
            videoFilter: videoFilter,
            outputFilePath: outputFileName
        );
    }

    public static List<string> BuildScreenshotGridAssemblyArguments(
        ScreenshotGridLayout layout,
        string tileFileNamePattern,
        IReadOnlyList<string> headerTextFileNames,
        string outputFilePath
    )
    {
        List<string> filters =
        [
            $"tile={layout.Columns}x{layout.Rows}:margin={layout.Spacing}:padding={layout.Spacing}:color={BackgroundColor}",
        ];

        if (layout.HeaderHeight > 0)
        {
            filters.Add(
                $"pad=iw:ih+{layout.HeaderHeight}:0:{layout.HeaderHeight}:color={BackgroundColor}"
            );
        }

        var headerTextOffset = (layout.HeaderLineHeight - layout.HeaderFontSize) / 2;

        for (var lineIndex = 0; lineIndex < headerTextFileNames.Count; lineIndex++)
        {
            var lineTop = layout.Spacing + lineIndex * layout.HeaderLineHeight + headerTextOffset;

            filters.Add(
                string.Join(
                    ':',
                    $"drawtext=fontfile={ScreenshotGridFont.FileName}",
                    $"textfile={headerTextFileNames[lineIndex]}",
                    "expansion=none",
                    "fontcolor=white",
                    $"fontsize={layout.HeaderFontSize}",
                    $"x={layout.Spacing * 2}",
                    $"y={lineTop}"
                )
            );
        }

        return
        [
            "-hide_banner",
            "-loglevel",
            "error",
            "-nostdin",
            "-framerate",
            "1",
            "-start_number",
            "1",
            "-i",
            tileFileNamePattern,
            "-vf",
            string.Join(',', filters),
            "-frames:v",
            "1",
            "-q:v",
            "2",
            "-update",
            "1",
            "-y",
            outputFilePath,
        ];
    }

    private static List<string> BuildFrameExtractionArguments(
        string videoFilePath,
        TimeSpan timestamp,
        string videoFilter,
        string outputFilePath
    )
    {
        return
        [
            "-hide_banner",
            "-loglevel",
            "error",
            "-nostdin",
            "-ss",
            timestamp.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i",
            videoFilePath,
            "-map",
            "0:v:0",
            "-frames:v",
            "1",
            "-an",
            "-sn",
            "-dn",
            "-vf",
            videoFilter,
            "-q:v",
            "2",
            "-update",
            "1",
            "-y",
            outputFilePath,
        ];
    }

    private static string BuildFrameFilter(VideoTransferCharacteristic transferCharacteristic)
    {
        return transferCharacteristic switch
        {
            VideoTransferCharacteristic.StandardDynamicRange => AnamorphicCorrectionFilter,
            VideoTransferCharacteristic.PerceptualQuantizer =>
                $"{PerceptualQuantizerLinearizationFilter},{ToneMappingToBt709Filter},{AnamorphicCorrectionFilter}",
            VideoTransferCharacteristic.HybridLogGamma =>
                $"{HybridLogGammaLinearizationFilter},{ToneMappingToBt709Filter},{AnamorphicCorrectionFilter}",
            _ => throw new ArgumentOutOfRangeException(
                nameof(transferCharacteristic),
                transferCharacteristic,
                null
            ),
        };
    }
}
