using System.IO.Ports;
using System.Text;
using MakroPad.Core.Protocol;

namespace MakroPad.Device;

public sealed class SerialMakroPadConnection : IMakroPadConnection, IDisposable
{
    private const int DefaultBaudRate = 115200;
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(6);

    private readonly object syncRoot = new();
    private SerialPort? serialPort;
    private CancellationTokenSource? readLoopCancellation;
    private Task? readLoopTask;
    private Task? heartbeatTask;
    private DateTimeOffset lastDeviceSeenAt = DateTimeOffset.MinValue;
    private int nextPingId;
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
                Encoding = Encoding.UTF8,
                NewLine = "\n",
                ReadTimeout = 250,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };

            serialPort.Open();
            State = DeviceConnectionState.Connected;
            lastDeviceSeenAt = DateTimeOffset.Now;

            readLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readLoopTask = Task.Run(() => ReadLoopAsync(readLoopCancellation.Token), CancellationToken.None);
            heartbeatTask = Task.Run(() => HeartbeatLoopAsync(readLoopCancellation.Token), CancellationToken.None);

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

        if (heartbeatTask is not null)
        {
            try
            {
                await heartbeatTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
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
        heartbeatTask = null;
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

        lock (syncRoot)
        {
            if (serialPort is not { IsOpen: true })
            {
                throw new InvalidOperationException("Serial port is not open.");
            }

            serialPort.WriteLine(line);
        }

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
                if (message is not null)
                {
                    MarkDeviceSeen();
                }

                if (message?.Type.Equals("hello", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await SendLineAsync("hello-ack;proto=1;app=0.1.0", cancellationToken);
                    continue;
                }

                if (message?.Type.Equals("ping", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var id = message.GetValue("id");
                    await SendLineAsync(string.IsNullOrWhiteSpace(id) ? "pong" : $"pong;id={id}", cancellationToken);
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
                CloseAfterReadFailure();
                break;
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && serialPort is { IsOpen: true })
        {
            try
            {
                await Task.Delay(HeartbeatInterval, cancellationToken);

                var elapsed = DateTimeOffset.Now - lastDeviceSeenAt;
                if (elapsed >= HeartbeatTimeout)
                {
                    if (State == DeviceConnectionState.Connected)
                    {
                        State = DeviceConnectionState.Unresponsive;
                        RaiseEvent("Connection", "Device heartbeat timed out");
                    }
                }

                if (elapsed >= HeartbeatInterval)
                {
                    var id = Interlocked.Increment(ref nextPingId);
                    await SendLineAsync($"ping;id={id}", cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException)
            {
                RaiseEvent("Connection", $"Heartbeat failed: {ex.Message}");
                CloseAfterReadFailure();
                break;
            }
        }
    }

    private void MarkDeviceSeen()
    {
        lastDeviceSeenAt = DateTimeOffset.Now;

        if (State == DeviceConnectionState.Unresponsive)
        {
            State = DeviceConnectionState.Connected;
            RaiseEvent("Connection", "Device heartbeat restored");
        }
    }

    private void CloseAfterReadFailure()
    {
        lock (syncRoot)
        {
            serialPort?.Close();
            serialPort?.Dispose();
            serialPort = null;
        }

        State = DeviceConnectionState.Disconnected;
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
