namespace MakroPad.Core.Display;

/// <summary>Port of the firmware's <c>display_text</c>: UTF-8 → CP437 and marquee scrolling.</summary>
public static class DisplayText
{
    private static readonly Dictionary<char, byte> Cp437 = new()
    {
        ['Ä'] = 0x8E, ['Ö'] = 0x99, ['Ü'] = 0x9A, ['ä'] = 0x84, ['ö'] = 0x94, ['ü'] = 0x81, ['ß'] = 0xE1,
        ['é'] = 0x82, ['è'] = 0x8A, ['ê'] = 0x88, ['á'] = 0xA0, ['à'] = 0x85, ['í'] = 0xA1, ['ó'] = 0xA2,
        ['ú'] = 0xA3, ['ñ'] = 0xA4, ['Ñ'] = 0xA5, ['ç'] = 0x87, ['É'] = 0x90, ['°'] = 0xF8, ['µ'] = 0xE6,
        ['–'] = (byte)'-', ['—'] = (byte)'-', ['‘'] = (byte)'\'', ['’'] = (byte)'\'',
        ['“'] = (byte)'"', ['”'] = (byte)'"', ['„'] = (byte)'"', ['…'] = (byte)'.'
    };

    public const int Gap = 3;

    public static byte[] ToCp437(string text)
    {
        var result = new List<byte>(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var c = text[index];
            if (c < 0x80)
            {
                result.Add((byte)c);
            }
            else if (Cp437.TryGetValue(c, out var mapped))
            {
                result.Add(mapped);
            }
            else
            {
                // A surrogate pair is one code point on the device, so it becomes a single '?'.
                if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    index++;
                }

                result.Add((byte)'?');
            }
        }

        return [.. result];
    }

    public static int MarqueeCycle(int length, int visible) => length > visible ? length + Gap : 0;

    public static byte[] MarqueeWindow(byte[] text, int visible, int step)
    {
        var cycle = MarqueeCycle(text.Length, visible);
        if (cycle == 0)
        {
            return text;
        }

        var window = new byte[visible];
        var start = step % cycle;
        for (var offset = 0; offset < visible; offset++)
        {
            var index = (start + offset) % cycle;
            window[offset] = index < text.Length ? text[index] : (byte)' ';
        }

        return window;
    }
}
