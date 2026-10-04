using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MakroPad.Actions;

internal sealed class WindowsKeyboardController : IKeyboardController
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyAlt = 0x12;
    private const ushort VirtualKeyWin = 0x5B;

    private static readonly Dictionary<string, ushort> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D,
        ["escape"] = 0x1B,
        ["esc"] = 0x1B,
        ["tab"] = 0x09,
        ["space"] = 0x20,
        ["backspace"] = 0x08,
        ["delete"] = 0x2E,
        ["insert"] = 0x2D,
        ["home"] = 0x24,
        ["end"] = 0x23,
        ["pageup"] = 0x21,
        ["pagedown"] = 0x22,
        ["up"] = 0x26,
        ["down"] = 0x28,
        ["left"] = 0x25,
        ["right"] = 0x27
    };

    public Task<ActionExecutionResult> SendShortcutAsync(
        string shortcut,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keys = ParseShortcut(shortcut);
        if (keys.Count == 0)
        {
            return Task.FromResult(ActionExecutionResult.Failed("Shortcut is empty or invalid"));
        }

        if (!TrySendShortcut(keys, out var errorMessage))
        {
            return Task.FromResult(ActionExecutionResult.Failed(errorMessage));
        }

        return Task.FromResult(ActionExecutionResult.Ok($"Shortcut sent: {shortcut}"));
    }

    private static List<ushort> ParseShortcut(string shortcut)
    {
        var keys = new List<ushort>();

        foreach (var rawPart in shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = rawPart.Trim();
            if (part.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("control", StringComparison.OrdinalIgnoreCase))
            {
                keys.Add(VirtualKeyControl);
                continue;
            }

            if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
            {
                keys.Add(VirtualKeyShift);
                continue;
            }

            if (part.Equals("alt", StringComparison.OrdinalIgnoreCase))
            {
                keys.Add(VirtualKeyAlt);
                continue;
            }

            if (part.Equals("win", StringComparison.OrdinalIgnoreCase)
                || part.Equals("windows", StringComparison.OrdinalIgnoreCase))
            {
                keys.Add(VirtualKeyWin);
                continue;
            }

            if (TryParseVirtualKey(part, out var virtualKey))
            {
                keys.Add(virtualKey);
            }
        }

        return keys;
    }

    private static bool TryParseVirtualKey(string key, out ushort virtualKey)
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

        if (key.Length is >= 2 and <= 3
            && key[0] is 'f' or 'F'
            && int.TryParse(key[1..], out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            virtualKey = (ushort)(0x70 + functionKey - 1);
            return true;
        }

        return NamedKeys.TryGetValue(key, out virtualKey);
    }

    private static bool TrySendShortcut(IReadOnlyList<ushort> keys, out string errorMessage)
    {
        var inputs = new List<Input>(keys.Count * 2);

        foreach (var key in keys)
        {
            inputs.Add(CreateKeyboardInput(key, 0));
        }

        for (var index = keys.Count - 1; index >= 0; index--)
        {
            inputs.Add(CreateKeyboardInput(keys[index], KeyEventKeyUp));
        }

        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        if (sent == inputs.Count)
        {
            errorMessage = "";
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        errorMessage = error == 0
            ? "SendInput failed"
            : new Win32Exception(error).Message;
        return false;
    }

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = flags
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;

        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;

        public int Y;

        public uint MouseData;

        public uint Flags;

        public uint Time;

        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;

        public ushort ScanCode;

        public uint Flags;

        public uint Time;

        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;

        public ushort LowParam;

        public ushort HighParam;
    }
}
