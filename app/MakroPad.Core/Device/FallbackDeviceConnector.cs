namespace MakroPad.Core.Device;

/// <summary>Tries each connector in order (e.g. USB before Bluetooth) and returns the first transport found.</summary>
public sealed class FallbackDeviceConnector(params IDeviceConnector[] connectors) : IDeviceConnector
{
    public async Task<ILineTransport?> ConnectAsync(CancellationToken cancellationToken)
    {
        foreach (var connector in connectors)
        {
            if (await connector.ConnectAsync(cancellationToken) is { } transport)
            {
                return transport;
            }
        }

        return null;
    }
}
