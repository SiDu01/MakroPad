using System.IO.Ports;
using MakroPad.Core.Device;
using MakroPad.Core.Protocol;

namespace MakroPad.Device;

/// <summary>
/// Finds the MakroPad by listening for its <c>hello</c> on every serial port, trying the
/// last successful port first.
/// </summary>
public sealed class SerialDeviceConnector : IDeviceConnector
{
    private static readonly TimeSpan ProbeWindow = TimeSpan.FromSeconds(2);

    private readonly Func<IEnumerable<string>> listPorts;

    public SerialDeviceConnector(Func<IEnumerable<string>>? listPorts = null)
    {
        this.listPorts = listPorts ?? SerialPort.GetPortNames;
    }

    /// <summary>Port of the last successful connection; probed first.</summary>
    public string? PreferredPort { get; set; }

    public async Task<ILineTransport?> ConnectAsync(CancellationToken cancellationToken)
    {
        foreach (var portName in OrderPorts(listPorts(), PreferredPort))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await Task.Run(() => IsMakroPad(portName, cancellationToken), cancellationToken))
            {
                continue;
            }

            try
            {
                var transport = SerialLineTransport.Open(portName);
                PreferredPort = portName;
                return transport;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
            }
        }

        return null;
    }

    public static IReadOnlyList<string> OrderPorts(IEnumerable<string> ports, string? preferredPort) => ports
        .Where(port => !string.IsNullOrWhiteSpace(port))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(port => string.Equals(port, preferredPort, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(port => port.Length)
        .ThenBy(port => port, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static bool IsMakroPadHello(string line)
    {
        try
        {
            var message = ProtocolCodec.Parse(line);
            return message.Type.Equals("hello", StringComparison.OrdinalIgnoreCase)
                   && string.Equals(message.GetValue("device"), DeviceMessages.DeviceName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }
    }

    private static bool IsMakroPad(string portName, CancellationToken cancellationToken)
    {
        try
        {
            using var port = SerialLineTransport.CreatePort(portName);
            port.Open();
            var deadline = DateTime.UtcNow + ProbeWindow;
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (IsMakroPadHello(port.ReadLine().Trim()))
                    {
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }

        return false;
    }
}
