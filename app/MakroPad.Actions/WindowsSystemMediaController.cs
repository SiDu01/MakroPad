using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MakroPad.Actions;

internal sealed class WindowsSystemMediaController : ISystemMediaController
{
    private const ushort VirtualKeyMediaPlayPause = 0xB3;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TrySendMediaKey(VirtualKeyMediaPlayPause, out var errorMessage))
        {
            return Task.FromResult(ActionExecutionResult.Failed(errorMessage));
        }

        return Task.FromResult(ActionExecutionResult.Ok("Play/Pause"));
    }

    private static bool TrySendMediaKey(ushort virtualKey, out string errorMessage)
    {
        var inputs = new Input[]
        {
            new()
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = virtualKey
                    }
                }
            },
            new()
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        Flags = KeyEventKeyUp
                    }
                }
            }
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == inputs.Length)
        {
            errorMessage = "";
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        errorMessage = error == 0
            ? "SendInput failed"
            : new Win32Exception(error).Message;
        return false;
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
        public KeyboardInput Keyboard;
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
