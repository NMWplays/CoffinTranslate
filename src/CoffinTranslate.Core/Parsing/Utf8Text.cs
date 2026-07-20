using System.Text;

namespace CoffinTranslate.Core.Parsing;

public static class Utf8Text
{
    /// <summary>
    /// Decodes bytes as UTF-8 (BOM tolerated). Reports whether the data was valid UTF-8 —
    /// invalid sequences are replaced so parsing can still proceed.
    /// </summary>
    public static string Decode(byte[] bytes, out bool isValidUtf8)
    {
        var span = bytes.AsSpan();
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            span = span[3..];

        try
        {
            isValidUtf8 = true;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(span);
        }
        catch (DecoderFallbackException)
        {
            isValidUtf8 = false;
            return Encoding.UTF8.GetString(span);
        }
    }
}
