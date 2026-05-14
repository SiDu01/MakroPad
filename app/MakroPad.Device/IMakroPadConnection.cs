namespace MakroPad.Device;

public interface IMakroPadConnection
{
    DeviceConnectionState State { get; }

    event EventHandler<DeviceEvent>? EventReceived;

    IReadOnlyList<string> GetAvailablePorts();

    Task ConnectAsync(string portName, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task SendDisplayMessageAsync(string text, CancellationToken cancellationToken = default);

    Task SendLineAsync(string line, CancellationToken cancellationToken = default);
}
