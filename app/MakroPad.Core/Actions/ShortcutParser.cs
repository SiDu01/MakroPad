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

    /// <summary>Canonical names used when recording, in the order <see cref="Format"/> writes them.</summary>
    private static readonly (ushort Key, string Name)[] ModifierNames =
    [
        (Control, "Ctrl"),
        (Shift, "Shift"),
        (Alt, "Alt"),
        (Win, "Win")
    ];

    private static readonly Dictionary<ushort, string> CanonicalNames = new()
    {
        [0x0D] = "Enter", [0x1B] = "Esc", [0x09] = "Tab", [0x20] = "Space", [0x08] = "Backspace",
        [0x2E] = "Delete", [0x2D] = "Insert", [0x24] = "Home", [0x23] = "End", [0x21] = "PageUp",
        [0x22] = "PageDown", [0x26] = "Up", [0x28] = "Down", [0x25] = "Left", [0x27] = "Right",
        [0x2C] = "PrintScreen", [0x13] = "Pause", [0x14] = "CapsLock", [0x90] = "NumLock",
        [0x91] = "ScrollLock", [0x5D] = "Apps", [0xBB] = "Plus", [0xBD] = "Minus", [0xBC] = "Comma", [0xBE] = "Period"
    };

    /// <summary>True for Ctrl/Shift/Alt/Win in their generic and left/right variants.</summary>
    public static bool IsModifier(ushort virtualKey) => ToModifier(virtualKey) is not null;

    /// <summary>Maps left/right modifier codes (e.g. VK_LCONTROL) to the generic code used in shortcuts.</summary>
    public static ushort? ToModifier(ushort virtualKey) => virtualKey switch
    {
        0x11 or 0xA2 or 0xA3 => Control,
        0x10 or 0xA0 or 0xA1 => Shift,
        0x12 or 0xA4 or 0xA5 => Alt,
        0x5B or 0x5C => Win,
        _ => null
    };

    /// <summary>Name of a non-modifier key in shortcut syntax, or null if shortcuts cannot use it.</summary>
    public static string? NameOf(ushort virtualKey) => virtualKey switch
    {
        >= 'A' and <= 'Z' or >= '0' and <= '9' => ((char)virtualKey).ToString(),
        >= 0x60 and <= 0x69 => ((char)('0' + (virtualKey - 0x60))).ToString(),
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x70 + 1}",
        _ => CanonicalNames.GetValueOrDefault(virtualKey)
    };

    /// <summary>Builds e.g. <c>Ctrl+Shift+Esc</c>; returns null if the main key has no name.</summary>
    public static string? Format(IEnumerable<ushort> modifiers, ushort mainKey)
    {
        if (NameOf(mainKey) is not { } name)
        {
            return null;
        }

        var held = modifiers.Select(m => ToModifier(m) ?? m).ToHashSet();
        return string.Join('+', ModifierNames.Where(m => held.Contains(m.Key)).Select(m => m.Name).Append(name));
    }

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
