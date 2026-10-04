namespace MakroPad.Core.Actions;

/// <summary>
/// Parses shortcuts like <c>Ctrl+Shift+Esc</c> into Windows virtual-key codes (modifiers first).
/// Pure so it can be validated without Windows.
/// </summary>
public static class ShortcutParser
{
    public const ushort Control = 0x11;
    public const ushort Shift = 0x10;
    public const ushort Alt = 0x12;
    public const ushort Win = 0x5B;

    private static readonly Dictionary<string, ushort> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = Control,
        ["control"] = Control,
        ["strg"] = Control,
        ["shift"] = Shift,
        ["alt"] = Alt,
        ["win"] = Win,
        ["windows"] = Win
    };

    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D,
        ["return"] = 0x0D,
        ["esc"] = 0x1B,
        ["escape"] = 0x1B,
        ["tab"] = 0x09,
        ["space"] = 0x20,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E,
        ["del"] = 0x2E,
        ["insert"] = 0x2D,
        ["home"] = 0x24,
        ["end"] = 0x23,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["up"] = 0x26,
        ["down"] = 0x28,
        ["left"] = 0x25,
        ["right"] = 0x27,
        ["printscreen"] = 0x2C,
        ["pause"] = 0x13,
        ["capslock"] = 0x14,
        ["numlock"] = 0x90,
        ["scrolllock"] = 0x91,
        ["apps"] = 0x5D,
        ["menu"] = 0x5D,
        ["plus"] = 0xBB,
        ["minus"] = 0xBD,
        ["comma"] = 0xBC,
        ["period"] = 0xBE
    };

    /// <summary>Keys that need KEYEVENTF_EXTENDEDKEY so they are not read as numpad keys.</summary>
    public static bool IsExtendedKey(ushort virtualKey) => virtualKey is
        0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2C or 0x2D or 0x2E or 0x5B or 0x5D or 0x90;

    public static bool TryParse(string shortcut, out IReadOnlyList<ushort> keys, out string error)
    {
        var result = new List<ushort>();
        keys = result;
        error = "";

        var parts = (shortcut ?? "").Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.All(string.IsNullOrEmpty))
        {
            error = "Tastenkombination ist leer";
            return false;
        }

        var mainKeys = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                error = $"Leerer Teil in \"{shortcut}\"";
                return false;
            }

            if (Modifiers.TryGetValue(part, out var modifier))
            {
                if (!result.Contains(modifier))
                {
                    result.Insert(result.Count - mainKeys, modifier);
                }

                continue;
            }

            if (!TryParseKey(part, out var key))
            {
                error = $"Unbekannte Taste \"{part}\"";
                return false;
            }

            result.Add(key);
            mainKeys++;
        }

        if (mainKeys == 0)
        {
            error = "Nur Modifikatoren, es fehlt eine Taste";
            return false;
        }

        return true;
    }

    private static bool TryParseKey(string key, out ushort virtualKey)
    {
        if (key.Length == 1)
        {
            var character = char.ToUpperInvariant(key[0]);
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = character;
                return true;
            }
        }

        if (key.Length is 2 or 3 && key[0] is 'f' or 'F' && int.TryParse(key[1..], out var function) && function is >= 1 and <= 24)
        {
            virtualKey = (ushort)(0x70 + function - 1);
            return true;
        }

        return NamedKeys.TryGetValue(key, out virtualKey);
    }
}
