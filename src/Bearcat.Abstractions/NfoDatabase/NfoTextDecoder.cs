using System.Text;

namespace Bearcat.Abstractions.NfoDatabase;

public static class NfoTextDecoder
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly Encoding LenientUtf8 = Encoding.GetEncoding(
        "UTF-8",
        EncoderFallback.ReplacementFallback,
        new DecoderReplacementFallback(string.Empty)
    );

    static NfoTextDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (HasUtf8Bom(bytes))
        {
            return LenientUtf8.GetString(bytes[3..]);
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(437).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return LenientUtf8.GetString(bytes);
        }
    }

    private static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }
}
