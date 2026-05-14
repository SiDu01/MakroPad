using System.IO.Ports;
using MakroPad.Core.Protocol;

namespace MakroPad.Device;

public sealed class SerialMakroPadConnection : IMakroPadConnection, IDisposable
{
    private const int DefaultBaudRate = 115200;

    private SerialPort? serialPort;
    private CancellationTokenSource? readLoopCancellation;
    private Task? readLoopTask;

    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Disconnected;

    public event EventHandler<DeviceEvent>? EventReceived;

    public IReadOnlyList<string> GetAvailablePorts()
    {
        return SerialPort
            .GetPortNames()
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        if (State == DeviceConnectionState.Connected)
        {
            return Task.CompletedTask;
        }

        State = DeviceConnectionState.Connecting;

        try
        {
            serialPort = new SerialPort(portName, DefaultBaudRate)
            {
                NewLine = "\n",
                ReadTimeout = 250,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };

            serialPort.Open();
            State = DeviceConnectionState.Connected;

            readLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readLoopTask = Task.Run(() => ReadLoopAsync(readLoopCancellation.Token), CancellationToken.None);

            RaiseEvent("Connection", $"Opened {portName}");
            return Task.CompletedTask;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            State = DeviceConnectionState.Disconnected;
            serialPort?.Dispose();
            serialPort = null;
            RaiseEvent("Connection", $"Failed to open {portName}: {ex.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == DeviceConnectionState.Disconnected)
        {
            return;
        }

        readLoopCancellation?.Cancel();

        if (readLoopTask is not null)
        {
            try
            {
                await readLoopTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        serialPort?.Close();
        serialPort?.Dispose();
        serialPort = null;
        readLoopTask = null;
        readLoopCancellation?.Dispose();
        readLoopCancellation = null;
        State = DeviceConnectionState.Disconnected;
        RaiseEvent("Connection", "Closed serial connection");
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

        return SendLineAsync(line, cancellationToken);
    }

    public Task SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (serialPort is not { IsOpen: true })
        {
            throw new InvalidOperationException("Serial port is not open.");
        }

        serialPort.WriteLine(line);
        RaiseEvent("App -> ESP", line);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        readLoopCancellation?.Cancel();
        serialPort?.Dispose();
        readLoopCancellation?.Dispose();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && serialPort is { IsOpen: true })
        {
            try
            {
                var line = serialPort.ReadLine().Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                RaiseEvent("ESP -> App", line);

                var message = TryParse(line);
                if (message?.Type.Equals("hello", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await SendLineAsync("hello-ack;proto=1;app=0.1.0", cancellationToken);
                }
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                RaiseEvent("Connection", $"Serial read failed: {ex.Message}");
                break;
            }
        }
    }

    private void RaiseEvent(string direction, string line)
    {
        EventReceived?.Invoke(this, new DeviceEvent(DateTimeOffset.Now, direction, line, TryParse(line)));
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
        catch (ArgumentException)
        {
            return null;
        }
    }
}
