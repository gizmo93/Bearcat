namespace Bearcat.Media.Screenshots;

public static class JpegDimensionReader
{
    private const byte MarkerPrefix = 0xFF;
    private const byte StartOfImage = 0xD8;

    public static (int Width, int Height) Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (reader.ReadByte() != MarkerPrefix || reader.ReadByte() != StartOfImage)
        {
            throw new InvalidDataException($"{filePath} is not a JPEG file.");
        }

        while (true)
        {
            if (reader.ReadByte() != MarkerPrefix)
            {
                throw new InvalidDataException($"{filePath} contains an invalid JPEG segment.");
            }

            var marker = reader.ReadByte();

            while (marker == MarkerPrefix)
            {
                marker = reader.ReadByte();
            }

            var segmentLength = ReadBigEndianUInt16(reader);

            if (IsStartOfFrame(marker))
            {
                reader.ReadByte();
                var height = ReadBigEndianUInt16(reader);
                var width = ReadBigEndianUInt16(reader);
                return (width, height);
            }

            stream.Seek(segmentLength - 2, SeekOrigin.Current);
        }
    }

    private static bool IsStartOfFrame(byte marker)
    {
        return marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
    }

    private static int ReadBigEndianUInt16(BinaryReader reader)
    {
        var highByte = reader.ReadByte();
        var lowByte = reader.ReadByte();
        return highByte << 8 | lowByte;
    }
}
