namespace MakroPad.Core.Device;

/// <summary>Finds a device and opens a transport to it.</summary>
public interface IDeviceConnector
{
    /// <summary>Returns an opened but not yet started transport, or null when no device was found.</summary>
    Task<ILineTransport?> ConnectAsync(CancellationToken cancellationToken);
}
