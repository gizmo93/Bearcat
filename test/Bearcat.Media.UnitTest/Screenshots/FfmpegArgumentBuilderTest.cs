using Bearcat.Abstractions.Media;
using Bearcat.Media.Screenshots;
using Shouldly;

namespace Bearcat.Media.UnitTest.Screenshots;

public class FfmpegArgumentBuilderTest
{
    [Test]
    public void BuildScreenshotArguments_Sdr_SeeksBeforeInputAndCorrectsAnamorphicVideo()
    {
        // Act
        var arguments = FfmpegArgumentBuilder.BuildScreenshotArguments(
            videoFilePath: "/videos/movie.mkv",
            timestamp: TimeSpan.FromSeconds(90.5),
            transferCharacteristic: VideoTransferCharacteristic.StandardDynamicRange,
            outputFilePath: "/screenshots/screenshot-01.jpg"
        );

        // Assert
        arguments.IndexOf("-ss").ShouldBeLessThan(arguments.IndexOf("-i"));
        arguments[arguments.IndexOf("-ss") + 1].ShouldBe("90.5");
        arguments[arguments.IndexOf("-i") + 1].ShouldBe("/videos/movie.mkv");
        arguments[arguments.IndexOf("-map") + 1].ShouldBe("0:v:0");
        arguments[arguments.IndexOf("-frames:v") + 1].ShouldBe("1");
        arguments[arguments.IndexOf("-vf") + 1].ShouldBe("scale=trunc(iw*sar/2)*2:ih,setsar=1");
        arguments[arguments.IndexOf("-q:v") + 1].ShouldBe("2");
        arguments.ShouldContain("-an");
        arguments.ShouldContain("-sn");
        arguments.ShouldContain("-dn");
        arguments.Last().ShouldBe("/screenshots/screenshot-01.jpg");
    }

    [Test]
    public void BuildScreenshotArguments_PerceptualQuantizer_LinearizesWithExplicitPqInputBeforeToneMapping()
    {
        // Act
        var arguments = FfmpegArgumentBuilder.BuildScreenshotArguments(
            videoFilePath: "/videos/movie.mkv",
            timestamp: TimeSpan.FromSeconds(90),
            transferCharacteristic: VideoTransferCharacteristic.PerceptualQuantizer,
            outputFilePath: "/screenshots/screenshot-01.jpg"
        );

        // Assert
        arguments[arguments.IndexOf("-vf") + 1]
            .ShouldBe(
                "zscale=tin=smpte2084:min=bt2020nc:pin=bt2020:rin=tv:t=linear:npl=100:p=bt2020,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p,scale=trunc(iw*sar/2)*2:ih,setsar=1"
            );
    }

    [Test]
    public void BuildScreenshotArguments_HybridLogGamma_LinearizesWithExplicitHlgInputBeforeToneMapping()
    {
        // Act
        var arguments = FfmpegArgumentBuilder.BuildScreenshotArguments(
            videoFilePath: "/videos/movie.mkv",
            timestamp: TimeSpan.FromSeconds(90),
            transferCharacteristic: VideoTransferCharacteristic.HybridLogGamma,
            outputFilePath: "/screenshots/screenshot-01.jpg"
        );

        // Assert
        arguments[arguments.IndexOf("-vf") + 1]
            .ShouldBe(
                "zscale=tin=arib-std-b67:min=bt2020nc:pin=bt2020:rin=tv:t=linear:npl=100:p=bt2020,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p,scale=trunc(iw*sar/2)*2:ih,setsar=1"
            );
    }

    [Test]
    public void BuildScreenshotGridTileArguments_ScalesToTileWidthAndDrawsTimestampFromTextFile()
    {
        // Arrange
        var layout = ScreenshotGridLayout.Calculate(
            columns: 4,
            rows: 4,
            sheetWidthPixels: 1920,
            headerLineCount: 0
        );

        // Act
        var arguments = FfmpegArgumentBuilder.BuildScreenshotGridTileArguments(
            videoFilePath: "/videos/movie.mkv",
            timestamp: TimeSpan.FromSeconds(30),
            transferCharacteristic: VideoTransferCharacteristic.StandardDynamicRange,
            layout: layout,
            timestampTextFileName: "timestamp-001.txt",
            outputFileName: "tile-001.jpg"
        );

        // Assert
        var videoFilter = arguments[arguments.IndexOf("-vf") + 1];
        videoFilter.ShouldStartWith("scale=trunc(iw*sar/2)*2:ih,setsar=1,scale=470:-2,drawtext=");
        videoFilter.ShouldContain("fontfile=NotoSans-Regular.ttf");
        videoFilter.ShouldContain("textfile=timestamp-001.txt");
        videoFilter.ShouldContain("expansion=none");
        arguments.Last().ShouldBe("tile-001.jpg");
    }

    [Test]
    public void BuildScreenshotGridAssemblyArguments_TilesFramesAndDrawsHeaderLinesAbove()
    {
        // Arrange
        var layout = ScreenshotGridLayout.Calculate(
            columns: 4,
            rows: 3,
            sheetWidthPixels: 1920,
            headerLineCount: 2
        );

        // Act
        var arguments = FfmpegArgumentBuilder.BuildScreenshotGridAssemblyArguments(
            layout: layout,
            tileFileNamePattern: "tile-%03d.jpg",
            headerTextFileNames: ["header-01.txt", "header-02.txt"],
            outputFilePath: "/screenshots/screenshot-grid.jpg"
        );

        // Assert
        arguments[arguments.IndexOf("-framerate") + 1].ShouldBe("1");
        arguments[arguments.IndexOf("-i") + 1].ShouldBe("tile-%03d.jpg");
        var filters = arguments[arguments.IndexOf("-vf") + 1].Split(',');
        filters.Length.ShouldBe(4);
        filters[0].ShouldBe("tile=4x3:margin=8:padding=8:color=0x202020");
        filters[1]
            .ShouldBe($"pad=iw:ih+{layout.HeaderHeight}:0:{layout.HeaderHeight}:color=0x202020");
        filters[2].ShouldContain("textfile=header-01.txt");
        filters[3].ShouldContain("textfile=header-02.txt");
        arguments.Last().ShouldBe("/screenshots/screenshot-grid.jpg");
    }
}
