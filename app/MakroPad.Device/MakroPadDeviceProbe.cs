using System.IO.Ports;
using System.Text;
using MakroPad.Core.Protocol;

namespace MakroPad.Device;

public sealed class MakroPadDeviceProbe
{
    private const int BaudRate = 115200;

    public async Task<string?> FindMakroPadPortAsync(
        IEnumerable<string> portNames,
        string? preferredPort = null,
        CancellationToken cancellationToken = default)
    {
        var orderedPorts = OrderPorts(portNames, preferredPort);

        foreach (var portName in orderedPorts)
        {
            if (await IsMakroPadPortAsync(portName, cancellationToken))
            {
                return portName;
            }
        }

        return null;
    }

    private static IEnumerable<string> OrderPorts(IEnumerable<string> portNames, string? preferredPort)
    {
        var uniquePorts = portNames
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(preferredPort))
        {
            return uniquePorts;
        }

        return uniquePorts
            .OrderBy(port => port.Equals(preferredPort, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(port => port, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<bool> IsMakroPadPortAsync(
        string portName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var port = new SerialPort(portName, BaudRate)
            {
                Encoding = Encoding.UTF8,
                NewLine = "\n",
                ReadTimeout = 250,
                WriteTimeout = 250,
                DtrEnable = true,
                RtsEnable = true
            };

            port.Open();

            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var line = port.ReadLine().Trim();
                    if (IsMakroPadHello(line))
                    {
                        return true;
                    }
                }
                catch (TimeoutException)
                {
                    await Task.Delay(25, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }

        return false;
    }

    private static bool IsMakroPadHello(string line)
    {
        try
        {
            var message = ProtocolCodec.Parse(line);
            return message.Type.Equals("hello", StringComparison.OrdinalIgnoreCase) &&
                   message.GetValue("device")?.Equals("makropad-s3", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }
    }
}
