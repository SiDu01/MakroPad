using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MakroPad.Windows;

/// <summary>Thin wrapper around user32 <c>SendInput</c> for virtual-key presses.</summary>
internal static class NativeInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;

    /// <summary>Presses and releases an extended key (media/volume keys).</summary>
    public static void TapExtendedKey(ushort virtualKey)
    {
        Send(
        [
            KeyInput(virtualKey, KeyEventExtendedKey),
            KeyInput(virtualKey, KeyEventExtendedKey | KeyEventKeyUp)
        ]);
    }

    /// <summary>Presses all keys in order, then releases them in reverse order.</summary>
    public static void SendChord(IReadOnlyList<ushort> virtualKeys, Func<ushort, bool> isExtended)
    {
        var inputs = new List<Input>(virtualKeys.Count * 2);
        foreach (var key in virtualKeys)
        {
            inputs.Add(KeyInput(key, isExtended(key) ? KeyEventExtendedKey : 0));
        }

        for (var index = virtualKeys.Count - 1; index >= 0; index--)
        {
            var key = virtualKeys[index];
            inputs.Add(KeyInput(key, (isExtended(key) ? KeyEventExtendedKey : 0) | KeyEventKeyUp));
        }

        Send([.. inputs]);
    }

    private static Input KeyInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = flags } }
    };

    private static void Send(Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(error == 0 ? "SendInput failed" : new Win32Exception(error).Message);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // The union must be as large as its biggest member (MOUSEINPUT) for SendInput to accept the size.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
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
}
