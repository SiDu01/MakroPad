using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MakroPad.Actions;

internal sealed class WindowsSystemMediaController : ISystemMediaController
{
    private const ushort VirtualKeyMediaPlayPause = 0xB3;
    private const ushort VirtualKeyMediaNextTrack = 0xB0;
    private const ushort VirtualKeyMediaPreviousTrack = 0xB1;
    private const ushort VirtualKeyMediaStop = 0xB2;
    private const ushort VirtualKeyVolumeMute = 0xAD;
    private const ushort VirtualKeyVolumeDown = 0xAE;
    private const ushort VirtualKeyVolumeUp = 0xAF;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventExtendedKey = 0x0001;

    public Task<ActionExecutionResult> PlayPauseAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyMediaPlayPause, "Play/Pause", cancellationToken);

    public Task<ActionExecutionResult> NextTrackAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyMediaNextTrack, "Next track", cancellationToken);

    public Task<ActionExecutionResult> PreviousTrackAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyMediaPreviousTrack, "Previous track", cancellationToken);

    public Task<ActionExecutionResult> StopAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyMediaStop, "Stop", cancellationToken);

    public Task<ActionExecutionResult> VolumeUpAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyVolumeUp, "Volume up", cancellationToken);

    public Task<ActionExecutionResult> VolumeDownAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyVolumeDown, "Volume down", cancellationToken);

    public Task<ActionExecutionResult> MuteAsync(CancellationToken cancellationToken = default)
        => SendMediaKeyAsync(VirtualKeyVolumeMute, "Mute", cancellationToken);

    private static Task<ActionExecutionResult> SendMediaKeyAsync(
        ushort virtualKey,
        string actionName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TrySendMediaKey(virtualKey, out var errorMessage))
        {
            return Task.FromResult(ActionExecutionResult.Failed(errorMessage));
        }

        return Task.FromResult(ActionExecutionResult.Ok(actionName));
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
                        VirtualKey = virtualKey,
                        Flags = KeyEventExtendedKey
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
                        Flags = KeyEventExtendedKey | KeyEventKeyUp
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

        var error = Marshal.GetLastWin32Error();
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
