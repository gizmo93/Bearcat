namespace Bearcat.Media.Screenshots;

public sealed record ScreenshotGridLayout(
    int Columns,
    int Rows,
    int Spacing,
    int TileWidth,
    int TimestampFontSize,
    int HeaderFontSize,
    int HeaderLineHeight,
    int HeaderHeight
)
{
    public static ScreenshotGridLayout Calculate(
        int columns,
        int rows,
        int sheetWidthPixels,
        int headerLineCount
    )
    {
        var spacing = Math.Max(4, sheetWidthPixels / 240);
        var availableTileWidth = (sheetWidthPixels - (columns + 1) * spacing) / columns;
        var tileWidth = availableTileWidth / 2 * 2;
        var headerFontSize = Math.Max(12, sheetWidthPixels / 64);
        var headerLineHeight = headerFontSize * 3 / 2;

        var headerHeight =
            headerLineCount == 0 ? 0 : 2 * spacing + headerLineCount * headerLineHeight;

        return new ScreenshotGridLayout(
            Columns: columns,
            Rows: rows,
            Spacing: spacing,
            TileWidth: tileWidth,
            TimestampFontSize: Math.Max(10, tileWidth / 20),
            HeaderFontSize: headerFontSize,
            HeaderLineHeight: headerLineHeight,
            HeaderHeight: (headerHeight + 1) / 2 * 2
        );
    }
}
