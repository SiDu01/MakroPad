using System.Globalization;
using MakroPad.Core.Device;
using MakroPad.Core.Protocol;

namespace MakroPad.Device;

/// <summary>
/// Software stand-in for the MakroPad: speaks protocol v2 so the whole app can run without
/// hardware. Inputs are injected with <see cref="PressButton"/> and <see cref="MoveSlider"/>.
/// </summary>
public sealed class SimulatedDevice : IDeviceConnector
{
    public const int Buttons = 16;
    public const int Sliders = 3;

    private Transport? current;

    /// <summary>Raised for every line the app sends to the device.</summary>
    public event Action<string>? LineFromApp;

    public Task<ILineTransport?> ConnectAsync(CancellationToken cancellationToken)
    {
        if (current is { IsOpen: true })
        {
            return Task.FromResult<ILineTransport?>(null);
        }

        current = new Transport(this);
        return Task.FromResult<ILineTransport?>(current);
    }

    public void PressButton(int id)
    {
        current?.Emit($"button;id={id};state=down");
        current?.Emit($"button;id={id};state=up");
    }

    public void ButtonDown(int id) => current?.Emit($"button;id={id};state=down");

    public void ButtonUp(int id) => current?.Emit($"button;id={id};state=up");

    public void MoveSlider(int id, int value)
        => current?.Emit($"slider;id={id};value={Math.Clamp(value, 0, SliderInput.MaxValue).ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Simulates unplugging; the app should search and reconnect.</summary>
    public void Disconnect() => current?.Dispose();

    private sealed class Transport(SimulatedDevice device) : ILineTransport
    {
        private int closed;

        public bool IsOpen => Volatile.Read(ref closed) == 0;

        public string Name => "Simulator";

        public event Action<string>? LineReceived;

        public event Action<Exception?>? Closed;

        public void Start() => Emit($"hello;proto={DeviceMessages.ProtocolVersion};fw=sim;device={DeviceMessages.DeviceName};buttons={Buttons};sliders={Sliders};display=ssd1306-128x64");

        public void SendLine(string line)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Simulator disconnected.");
            }

            device.LineFromApp?.Invoke(line);
            var message = ProtocolCodec.Parse(line);
            if (message.Type == "ping")
            {
                Emit(DeviceMessages.Pong(message.GetValue("id")));
            }
        }

        public void Emit(string line)
        {
            if (IsOpen)
            {
                LineReceived?.Invoke(line);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                Closed?.Invoke(null);
            }
        }
    }
}
