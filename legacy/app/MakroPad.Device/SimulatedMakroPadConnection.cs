using MakroPad.Core.Protocol;

namespace MakroPad.Device;

public sealed class SimulatedMakroPadConnection : IMakroPadConnection
{
    private DeviceConnectionState state = DeviceConnectionState.Disconnected;

    public DeviceConnectionState State
    {
        get => state;
        private set
        {
            if (state == value)
            {
                return;
            }

            state = value;
            StateChanged?.Invoke(this, state);
        }
    }

    public event EventHandler<DeviceEvent>? EventReceived;

    public event EventHandler<DeviceConnectionState>? StateChanged;

    public IReadOnlyList<string> GetAvailablePorts()
    {
        return ["Simulator"];
    }

    public async Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        State = DeviceConnectionState.Connecting;
        await Task.Delay(150, cancellationToken);

        State = DeviceConnectionState.Connected;
        Receive("hello;proto=1;fw=0.2.0;device=makropad-s3;buttons=20;sliders=1;display=ssd1306-128x64");
        Send("hello-ack;proto=1;app=0.1.0");
        Receive("ready;uptime=812");
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = DeviceConnectionState.Disconnected;
        return Task.CompletedTask;
    }

    public Task SendDisplayMessageAsync(string text, CancellationToken cancellationToken = default)
    {
        var line = ProtocolCodec.Format(
            "display",
            new Dictionary<string, string?>
            {
                ["mode"] = "message",
                ["text"] = text
            });

        Send(line);
        return Task.CompletedTask;
    }

    public Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        Send(line);
        return Task.CompletedTask;
    }

    public void SimulateButtonPress(int id)
    {
        Receive($"button;id={id};state=down");
        Receive($"button;id={id};state=up");
    }

    public void SimulateSliderChange(int id, int value)
    {
        Receive($"slider;id={id};value={value};raw={value}");
    }

    private void Send(string line)
    {
        EventReceived?.Invoke(this, new DeviceEvent(DateTimeOffset.Now, "App -> ESP", line, TryParse(line)));
    }

    private void Receive(string line)
    {
        EventReceived?.Invoke(this, new DeviceEvent(DateTimeOffset.Now, "ESP -> App", line, TryParse(line)));
    }

    private static ProtocolMessage? TryParse(string line)
    {
        try
        {
            return ProtocolCodec.Parse(line);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
